using System.Collections.Concurrent;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryManager(
    ILibraryRepository libraryRepository,
    JobDispatcher jobDispatcher
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

        foreach (string path in paths)
        {
            Logger.App("Scanning " + path);
            switch (_library.Type)
            {
                case "music":
                    await ScanAudioFolder(path, depth);
                    break;
                case "anime":
                case "tv":
                case "movie":
                    await ScanVideoFolder(path, depth);
                    break;
            }
        }

        Logger.App("Scanning done");
    }

    private async Task ScanVideoFolder(string path, int depth)
    {
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .Process(path, depth);

        foreach (MediaFolderExtend folder in rootFolders.OrderBy(f => f.Path)) await ProcessVideoFolder(folder);

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

        foreach (MediaFolderExtend rootFolder in rootFolders.OrderBy(f => f.Path))
        {
            ProcessMusicFolder(rootFolder);
        }

        Logger.App("Found " + rootFolders.Count + " subfolders");
    }

    private async Task ProcessVideoFolder(MediaFolderExtend path)
    {
        if (_library is null) return;
        switch (_library.Type)
        {
            case "movie":
            {
                await ProcessMovieFolder(path);
                break;
            }
            case "anime":
            case "tv":
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

        if (paginatedMovieResponse?.Results.Length <= 0) return;

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

        if (paginatedTvShowResponse?.Results.Length <= 0) return;

        // List<TvShow> res = Str.SortByMatchPercentage(paginatedTvShowResponse.Results, m => m.Name, folder.Parsed.Title);
        IEnumerable<TmdbTvShow> res = paginatedTvShowResponse?.Results ?? [];
        if (res.Count() is 0) return;

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
            "movie" or "tv" or "anime" => 1,
            "music" => 2,
            _ => 1
        };
    }
}
