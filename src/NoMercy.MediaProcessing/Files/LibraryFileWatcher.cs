using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

public class LibraryFileWatcher
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<LibraryFileWatcher> _instance = new(() => new());
    public static LibraryFileWatcher Instance => _instance.Value;

    private static readonly FolderWatcher Fs = new();

    private static readonly Dictionary<string, FileChangeGroup> FileChangeGroups = new();
    private static readonly Lock LockObject = new();

    private static readonly Regex EncodingOutputRegex = new(
        @"^(video_.*|audio_.*|subtitles|fonts|thumbs|metadata|scans|cds.*|NCED|NCOP)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int Delay = 10;

    private static List<Library> _libraries = [];

    public LibraryFileWatcher()
    {
        Logger.System("Starting FileSystem Watcher", LogEventLevel.Debug);

        Fs.OnChanged += _onFileChanged;
        Fs.OnCreated += _onFileCreated;
        Fs.OnDeleted += _onFileDeleted;
        Fs.OnRenamed += _onFileRenamed;
        Fs.OnError += _onError;

        RefreshLibraryCache();
        Parallel.ForEach(_libraries, library => AddLibraryWatcher(library));
    }

    public static void RefreshLibraryCache()
    {
        using MediaContext mediaContext = new();
        _libraries = mediaContext.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .ToList();
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

    private void _onFileChanged(FileWatcherEventArgs e) => HandleFileChange(e);
    private void _onFileCreated(FileWatcherEventArgs e) => HandleFileChange(e);
    private void _onFileDeleted(FileWatcherEventArgs e) => HandleFileChange(e);
    private void _onFileRenamed(FileWatcherEventArgs e) => HandleFileChange(e);

    private void _onError(FileWatcherEventArgs e)
    {
        Logger.System(e, LogEventLevel.Error);
    }

    private static Library? GetLibraryByPath(string path)
    {
        return _libraries.FirstOrDefault(library => library.FolderLibraries
            .Any(folderLibrary => path.Contains(folderLibrary.Folder.Path)));
    }

    private static bool IsInEncodingOutputDirectory(string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(directory))
        {
            string dirName = Path.GetFileName(directory);
            if (EncodingOutputRegex.IsMatch(dirName))
                return true;

            directory = Path.GetDirectoryName(directory);
        }
        return false;
    }

    private void HandleFileChange(FileWatcherEventArgs e)
    {
        if (IsInEncodingOutputDirectory(e.FullPath)) return;

        string watcherPath = e.Path;
        Library? library = GetLibraryByPath(watcherPath);

        if (library is null) return;

        if (!IsAllowedExtensionForLibrary(library, e.FullPath)) return;

        if (e.ChangeType != WatcherChangeTypes.Deleted && !Path.Exists(e.FullPath)) return;

        string folderPath = Path.GetDirectoryName(e.FullPath) ?? string.Empty;

        if (string.IsNullOrEmpty(folderPath)) return;

        lock (LockObject)
        {
            if (!FileChangeGroups.TryGetValue(folderPath, out FileChangeGroup? fileChangeGroup))
            {
                fileChangeGroup = new(e.ChangeType, library, folderPath);
                FileChangeGroups[folderPath] = fileChangeGroup;
            }

            fileChangeGroup.FullPath = e.FullPath;
            fileChangeGroup.ChangeType = e.ChangeType;

            if (e.ChangeType == WatcherChangeTypes.Renamed && e.OldFullPath is not null)
                fileChangeGroup.OldFullPath = e.OldFullPath;

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

        FileChangeGroup snapshot;
        lock (LockObject)
        {
            snapshot = new(group.ChangeType, group.Library, group.FolderPath)
            {
                FullPath = group.FullPath,
                OldFullPath = group.OldFullPath
            };
            FileChangeGroups.Remove(group.FolderPath);
        }

        Task.Run(async () =>
        {
            try
            {
                await PublishFileEvent(snapshot);
            }
            catch (Exception ex)
            {
                Logger.System($"FileWatcher error processing {snapshot.FolderPath}: {ex.Message}", LogEventLevel.Error);
            }
        });
    }

    private static async Task PublishFileEvent(FileChangeGroup group)
    {
        if (!EventBusProvider.IsConfigured) return;

        switch (group.ChangeType)
        {
            case WatcherChangeTypes.Created:
            case WatcherChangeTypes.Changed:
                Logger.System($"FileWatcher: Publishing FileCreatedEvent for {group.FolderPath}", LogEventLevel.Debug);
                await EventBusProvider.Current.PublishAsync(new FileCreatedEvent
                {
                    FolderPath = group.FolderPath,
                    LibraryId = group.Library.Id,
                    LibraryType = group.Library.Type
                });
                break;

            case WatcherChangeTypes.Deleted:
                Logger.System($"FileWatcher: Publishing FileDeletedEvent for {group.FullPath}", LogEventLevel.Debug);
                await EventBusProvider.Current.PublishAsync(new FileDeletedEvent
                {
                    FullPath = group.FullPath ?? group.FolderPath,
                    LibraryId = group.Library.Id,
                    LibraryType = group.Library.Type
                });
                break;

            case WatcherChangeTypes.Renamed when group.OldFullPath is not null:
                Logger.System($"FileWatcher: Publishing FileRenamedEvent from {group.OldFullPath} to {group.FullPath}", LogEventLevel.Debug);
                await EventBusProvider.Current.PublishAsync(new FileRenamedEvent
                {
                    OldFullPath = group.OldFullPath,
                    NewFullPath = group.FullPath ?? group.FolderPath,
                    LibraryId = group.Library.Id,
                    LibraryType = group.Library.Type
                });
                break;

            case WatcherChangeTypes.Renamed:
                Logger.System($"FileWatcher: Rename detected but no OldFullPath, treating as Created for {group.FolderPath}", LogEventLevel.Debug);
                await EventBusProvider.Current.PublishAsync(new FileCreatedEvent
                {
                    FolderPath = group.FolderPath,
                    LibraryId = group.Library.Id,
                    LibraryType = group.Library.Type
                });
                break;
        }
    }

    public static void Start()
    {
        _ = Instance;
    }
}
