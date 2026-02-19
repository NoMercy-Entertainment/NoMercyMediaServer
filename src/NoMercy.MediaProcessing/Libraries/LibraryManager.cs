using System.Collections.Concurrent;
using System.Diagnostics;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
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
    MediaContext mediaContext,
    IEventBus? eventBus = null
)
    : BaseManager, ILibraryManager
{
    private Library? _library;
    private readonly IEventBus? _eventBus = eventBus;

    public async Task ProcessLibrary(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id);
        if (_library is null) return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsFound = 0;

        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null)
        {
            await bus.PublishAsync(new LibraryScanStartedEvent
            {
                LibraryId = _library.Id,
                LibraryName = _library.Title
            });
        }

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
                    int audioCount = await ScanAudioFolder(path, depth);
                    Interlocked.Add(ref itemsFound, audioCount);
                    break;
                case Config.AnimeMediaType:
                case Config.TvMediaType:
                case Config.MovieMediaType:
                    int videoCount = await ScanVideoFolder(path, depth);
                    Interlocked.Add(ref itemsFound, videoCount);
                    break;
            }
        });

        stopwatch.Stop();

        if (bus is not null)
        {
            await bus.PublishAsync(new LibraryScanCompletedEvent
            {
                LibraryId = _library.Id,
                LibraryName = _library.Title,
                ItemsFound = itemsFound,
                Duration = stopwatch.Elapsed
            });
        }

        Logger.App("Scanning done");
    }

    public async Task ProcessNewLibraryItems(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id);
        if (_library is null) return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsFound = 0;

        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null)
        {
            await bus.PublishAsync(new LibraryScanStartedEvent
            {
                LibraryId = _library.Id,
                LibraryName = _library.Title
            });
        }

        HashSet<string> existingFolders = await libraryRepository.GetExistingFolderNamesAsync(id, _library.Type);

        List<string> paths = [];
        paths.AddRange(_library.FolderLibraries.Select(folderLibrary => folderLibrary.Folder.Path));

        int depth = GetDepth();

        await Parallel.ForEachAsync(paths, Config.ParallelOptions, async (path, _) =>
        {
            Logger.App("Scanning for new items in " + path);
            switch (_library.Type)
            {
                case Config.MusicMediaType:
                    int audioCount = await ScanNewAudioFolder(path, depth, existingFolders);
                    Interlocked.Add(ref itemsFound, audioCount);
                    break;
                case Config.AnimeMediaType:
                case Config.TvMediaType:
                case Config.MovieMediaType:
                    int videoCount = await ScanNewVideoFolder(path, depth, existingFolders);
                    Interlocked.Add(ref itemsFound, videoCount);
                    break;
            }
        });

        stopwatch.Stop();

        if (bus is not null)
        {
            await bus.PublishAsync(new LibraryScanCompletedEvent
            {
                LibraryId = _library.Id,
                LibraryName = _library.Title,
                ItemsFound = itemsFound,
                Duration = stopwatch.Elapsed
            });
        }

        Logger.App($"Scan for new items done — {itemsFound} new items found");
    }

    private async Task<int> ScanNewVideoFolder(string path, int depth, HashSet<string> existingFolders)
    {
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan.Process(path, depth);

        List<MediaFolderExtend> newFolders = rootFolders
            .Where(f => !existingFolders.Contains(f.Name.NormalizeForComparison()))
            .ToList();

        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend folder in newFolders)
            {
                await bus.PublishAsync(new MediaDiscoveredEvent
                {
                    FilePath = folder.Path,
                    LibraryId = _library.Id,
                    DetectedType = _library.Type
                });
            }
        }

        await Parallel.ForEachAsync(newFolders.OrderBy(f => f.Path), Config.ParallelOptions, async (rootFolder, _) =>
        {
            await ProcessVideoFolder(rootFolder);
        });

        Logger.App($"Found {newFolders.Count} new subfolders (skipped {rootFolders.Count - newFolders.Count} existing)");
        return newFolders.Count;
    }

    private async Task<int> ScanNewAudioFolder(string path, int depth, HashSet<string> existingFolders)
    {
        await using MediaScan mediaScan = new();
        List<MediaFolderExtend> rootFolders = (await mediaScan
                .DisableRegexFilter()
                .Process(path, depth))
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        List<MediaFolderExtend> newFolders = rootFolders
            .Where(f => !existingFolders.Contains(f.Name.NormalizeForComparison()))
            .ToList();

        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend folder in newFolders)
            {
                await bus.PublishAsync(new MediaDiscoveredEvent
                {
                    FilePath = folder.Path,
                    LibraryId = _library.Id,
                    DetectedType = _library.Type
                });
            }
        }

        Parallel.ForEach(newFolders.OrderBy(f => f.Path), Config.ParallelOptions, (rootFolder, _) =>
        {
            ProcessMusicFolder(rootFolder);
        });

        Logger.App($"Found {newFolders.Count} new subfolders (skipped {rootFolders.Count - newFolders.Count} existing)");
        return newFolders.Count;
    }

    private async Task<int> ScanVideoFolder(string path, int depth)
    {
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .Process(path, depth);

        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend folder in rootFolders)
            {
                await bus.PublishAsync(new MediaDiscoveredEvent
                {
                    FilePath = folder.Path,
                    LibraryId = _library.Id,
                    DetectedType = _library.Type
                });
            }
        }

        await Parallel.ForEachAsync(rootFolders.OrderBy(f => f.Path), Config.ParallelOptions, async (rootFolder, _) =>
        {
            await ProcessVideoFolder(rootFolder);
        });

        Logger.App("Found " + rootFolders.Count + " subfolders");
        return rootFolders.Count;
    }

    private async Task<int> ScanAudioFolder(string path, int depth)
    {
        await using MediaScan mediaScan = new();
        List<MediaFolderExtend> rootFolders = (await mediaScan
                .DisableRegexFilter()
                .Process(path, depth))
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend folder in rootFolders)
            {
                await bus.PublishAsync(new MediaDiscoveredEvent
                {
                    FilePath = folder.Path,
                    LibraryId = _library.Id,
                    DetectedType = _library.Type
                });
            }
        }

        Parallel.ForEach(rootFolders.OrderBy(f => f.Path), Config.ParallelOptions, (rootFolder, _) =>
        {
            ProcessMusicFolder(rootFolder);
        });

        Logger.App("Found " + rootFolders.Count + " subfolders");
        return rootFolders.Count;
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

        jobDispatcher.DispatchJob<MovieImportJob>(res.First().Id, _library);
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

        jobDispatcher.DispatchJob<ShowImportJob>(res.First().Id, _library);
    }

    private void ProcessMusicFolder(MediaFolderExtend baseFolderExtend)
    {
        if (_library is null) return;

        jobDispatcher.DispatchJob<ReleaseImportJob>(baseFolderExtend.Path, _library.Id);
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