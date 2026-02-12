using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

public class LibraryFileWatcher
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<LibraryFileWatcher> _instance = new(() => new());
    public static LibraryFileWatcher Instance => _instance.Value;

    private static readonly MediaContext MediaContext = new();
    private static readonly FolderWatcher Fs = new();

    private static readonly Dictionary<string, FileChangeGroup> FileChangeGroups = new();
    private static readonly Lock LockObject = new();

    private static readonly JobDispatcher JobDispatcher = new();
    private static readonly FileRepository FileRepository = new(MediaContext);
    private static readonly FileManager FileManager = new(FileRepository);

    private const int Delay = 10;

    public LibraryFileWatcher()
    {
        Logger.System("Starting FileSystem Watcher", LogEventLevel.Debug);

        Fs.OnChanged += _onFileChanged;
        Fs.OnCreated += _onFileCreated;
        Fs.OnDeleted += _onFileDeleted;
        Fs.OnRenamed += _onFileRenamed;
        Fs.OnError += _onError;

        List<Library> libraries = MediaContext.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .ToList();
        Parallel.ForEach(libraries, library => AddLibraryWatcher(library));
        // foreach (Library library in libraries) AddLibraryWatcher(library);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static Action AddLibraryWatcher(Library library)
    {
        List<string> paths = library.FolderLibraries
            .Select(folderLibrary => folderLibrary.Folder.Path)
            .ToList();
        
        List<Action> disposers = [];

        Task.Run(() =>
        {
            disposers = Fs.Watch(paths);
        }).Wait();

        return () =>
        {
            foreach (Action disposer in disposers)
                disposer();
        };
    }

    private void _onFileChanged(FileWatcherEventArgs e)
    {
        HandleFileChange(e);
    }

    private void _onFileCreated(FileWatcherEventArgs e)
    {
        HandleFileChange(e);
    }

    private void _onFileDeleted(FileWatcherEventArgs e)
    {
        HandleFileChange(e);
    }

    private void _onFileRenamed(FileWatcherEventArgs e)
    {
        HandleFileChange(e);
    }

    private void _onError(FileWatcherEventArgs e)
    {
        Logger.System(e, LogEventLevel.Error);
    }

    private Library? GetLibraryByPath(string path)
    {
        return MediaContext.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .FirstOrDefault(library => library.FolderLibraries
                .Any(folderLibrary => path.Contains(folderLibrary.Folder.Path)));
    }

    private void HandleFileChange(FileWatcherEventArgs e)
    {
        string watcherPath = e.Path;
        Library? library = GetLibraryByPath(watcherPath);

        if (library is null) return;

        if (!IsAllowedExtensionForLibrary(library, e.FullPath)) return;
        
        if (!Path.Exists(e.FullPath)) return;
        
        string folderPath = Path.GetDirectoryName(e.FullPath) ?? string.Empty;
        
        if (string.IsNullOrEmpty(folderPath)) return;
        
        lock (LockObject)
        {
            if (!FileChangeGroups.TryGetValue(folderPath, out FileChangeGroup? fileChangeGroup))
            {
                fileChangeGroup = new(e.ChangeType, library, folderPath);
                FileChangeGroups[folderPath] = fileChangeGroup;
            }

            fileChangeGroup.Timer?.Dispose();
            fileChangeGroup.Timer = new(ProcessFileChanges, fileChangeGroup, TimeSpan.FromSeconds(Delay),
                Timeout.InfiniteTimeSpan);
        }
    }

    private static bool IsAllowedExtensionForLibrary(Library library, string path)
    {
        if (Directory.Exists(path)) return true;
        
        switch (library.Type)
        {
            case Config.MovieMediaType:
            case Config.TvMediaType:
                string[] videoExtensions = [".mp4", ".mkv", ".avi", ".webm", ".mov", ".m3u8"];
                return videoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            case Config.MusicMediaType:
                string[] audioExtensions = [".mp3", ".flac", ".opus", ".wav", ".m4a"];
                return audioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    private void ProcessFileChanges(object? state)
    {
        if (state is not FileChangeGroup group)
            return;

        lock (LockObject)
        {
            switch (group.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Renamed:
                    _ = HandleFolder(group.Library, group.FolderPath);
                    break;
                case WatcherChangeTypes.All:
                default:
                    break;
            }
            FileChangeGroups.Remove(group.FolderPath);
        }
    }

    private async Task HandleFolder(Library library, string path)
    {
        MediaScan mediaScan = new();
        MediaScan scan = mediaScan.EnableFileListing();

        if (library.Type == Config.MusicMediaType)
            scan.DisableRegexFilter();

        IEnumerable<MediaFolderExtend> mediaFolder = await scan.Process(path);

        switch (library.Type)
        {
            case Config.MovieMediaType:
                await HandleMovieFolder(library, mediaFolder.First());
                break;
            case Config.TvMediaType:
                await HandleTvFolder(library, mediaFolder.First());
                break;
            case Config.MusicMediaType:
                HandleMusicFolder(library, mediaFolder.First());
                break;
        }
    }

    private void HandleMusicFolder(Library library, MediaFolderExtend path)
    {
        Logger.System($"Music {path.Path}: Processing");

        // JobDispatcher.DispatchJob<ProcessReleaseFolderJob>(path.Path, library.Id);
        string directoryPath = Path.GetFullPath(path.Path);
        FolderLibrary? folderLibrary =
            library.FolderLibraries.FirstOrDefault(f => directoryPath.Contains(f.Folder.Path));
        if (folderLibrary is null) return;
        
        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AudioImportJob>(library.Id, folderLibrary.FolderId, directoryPath);
    }

    private async Task HandleTvFolder(Library library, MediaFolderExtend path)
    {
        Logger.System($"Tv Show {path.Path}: Processing");

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? paginatedTvShowResponse =
            await tmdbSearchClient.TvShow(path.Parsed.Title!, path.Parsed.Year);

        if (paginatedTvShowResponse?.Results.Count <= 0) return;

        IEnumerable<TmdbTvShow> res = paginatedTvShowResponse?.Results ?? [];
        if (res.Count() is 0) return;

        Logger.System($"Tv Show {res.First().Name}: Found {res.First().Name}");

        await FileManager.FindFiles(res.First().Id, library);

        JobDispatcher.DispatchJob<AddShowJob>(res.First().Id, library);
    }

    private async Task HandleMovieFolder(Library library, MediaFolderExtend path)
    {
        Logger.System($"Movie {path.Path}: Processing");

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? paginatedTvShowResponse =
            await tmdbSearchClient.Movie(path.Parsed.Title!, path.Parsed.Year);

        if (paginatedTvShowResponse?.Results.Count <= 0) return;

        IEnumerable<TmdbMovie> res = paginatedTvShowResponse?.Results ?? [];
        if (res.Count() is 0) return;

        Logger.System($"Movie {res.First().Title}: Found {res.First().Title}");

        await FileManager.FindFiles(res.First().Id, library);

        JobDispatcher.DispatchJob<AddMovieJob>(res.First().Id, library);
    }

    public static void Start()
    {
        _ = Instance;
    }
}