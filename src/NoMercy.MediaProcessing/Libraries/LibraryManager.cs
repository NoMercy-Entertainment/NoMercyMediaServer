using System.Collections.Concurrent;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryManager(
    LibraryRepository libraryRepository,
    JobDispatcher jobDispatcher,
    MediaContext mediaContext
)
    : BaseManager, ILibraryManager
{
    private Library? _library;

    public async Task ProcessLibrary(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id);
        if (_library is null) return;

        List<string> paths = [];

        paths.AddRange(_library.FolderLibraries
            .Select(folderLibrary => folderLibrary.Folder.Path));

        int depth = GetDepth();

        await Parallel.ForEachAsync(paths, Config.ParallelOptions, async (path, _) =>
        {
            Logger.App("Scanning " + path);
            switch (_library.Type)
            {
                case Config.MusicMediaType:
                    await ScanAudioFolder(path, depth);
                    break;
                case Config.AnimeMediaType:
                case Config.TvMediaType:
                case Config.MovieMediaType:
                    await ScanVideoFolder(path, depth);
                    break;
            }
        });

        Logger.App("Scanning done");
    }

    private async Task ScanVideoFolder(string path, int depth)
    {
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .Process(path, depth);
        
        await Parallel.ForEachAsync(rootFolders.OrderBy(f => f.Path), Config.ParallelOptions, async (rootFolder, _) =>
        {
            await ProcessVideoFolder(rootFolder);
        });

        Logger.App("Found " + rootFolders.Count + " subfolders");
    }

    private async Task ScanAudioFolder(string path, int depth)
    {
        await using MediaScan mediaScan = new();
        List<MediaFolderExtend> rootFolders = (await mediaScan
                .DisableRegexFilter()
                .Process(path, depth))
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        Parallel.ForEach(rootFolders.OrderBy(f => f.Path), Config.ParallelOptions, (rootFolder, _) =>
        {
            ProcessMusicFolder(rootFolder);
        });

        Logger.App("Found " + rootFolders.Count + " subfolders");
    }

    private async Task ProcessVideoFolder(MediaFolderExtend path)
    {
        if (_library is null) return;
        switch (_library.Type)
        {
            case Config.MovieMediaType:
            {
                await ProcessMovieFolder(path);
                break;
            }
            case Config.AnimeMediaType:
            case Config.TvMediaType:
            {
                await ProcessTvFolder(path);
                break;
            }
        }
    }

    private async Task ProcessMovieFolder(MediaFolderExtend folderExtend)
    {
        if (_library is null) return;

        Logger.App("Processing movie folder " + folderExtend.Path);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? paginatedMovieResponse =
            await tmdbSearchClient.Movie(folderExtend.Parsed.Title!, folderExtend.Parsed.Year);

        if (paginatedMovieResponse?.Results.Count <= 0) return;

        // List<Movie> res = Str.SortByMatchPercentage(paginatedMovieResponse?.Results, m => m.Title, folder.Parsed.Title);
        IEnumerable<TmdbMovie> res = paginatedMovieResponse?.Results ?? [];
        if (res.Count() is 0) return;

        jobDispatcher.DispatchJob<AddMovieJob>(res.First().Id, _library);
    }

    private async Task ProcessTvFolder(MediaFolderExtend folderExtend)
    {
        if (_library is null) return;

        Logger.App("Processing tv folder " + folderExtend.Path);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? paginatedTvShowResponse =
            await tmdbSearchClient.TvShow(folderExtend.Parsed.Title!, folderExtend.Parsed.Year);

        if (paginatedTvShowResponse?.Results.Count <= 0) return;

        // List<TvShow> res = Str.SortByMatchPercentage(paginatedTvShowResponse.Results, m => m.Name, folder.Parsed.Title);
        IEnumerable<TmdbTvShow> res = paginatedTvShowResponse?.Results ?? [];
        if (!res.Any()) return;

        jobDispatcher.DispatchJob<AddShowJob>(res.First().Id, _library);
    }

    private void ProcessMusicFolder(MediaFolderExtend baseFolderExtend)
    {
        if (_library is null) return;

        jobDispatcher.DispatchJob<ProcessReleaseFolderJob>(baseFolderExtend.Path, _library.Id);
    }

    private int GetDepth()
    {
        if (_library is null) return 0;

        return _library.Type switch
        {
            Config.MovieMediaType or Config.TvMediaType or Config.AnimeMediaType => 1,
            Config.MusicMediaType => 2,
            _ => 1
        };
    }

    public void Dispose()
    {
        libraryRepository.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await libraryRepository.DisposeAsync();
    }

    public async Task RescanFiles(Ulid libraryId, int id)
    {
        Library? library = await libraryRepository.GetLibraryByIdWithFolders(libraryId);
        if (library is null)
        {
            Logger.App("Library with ID " + libraryId + " not found", LogEventLevel.Warning);
            return;
        }
        
        FileRepository fileRepository = new(mediaContext);
        FileManager fileManager = new(fileRepository);

        await fileManager.FindFiles(id, library);
    }
}