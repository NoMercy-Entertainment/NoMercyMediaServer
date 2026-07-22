// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.Analysis;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryManager(
    LibraryRepository libraryRepository,
    JobDispatcher jobDispatcher,
    MediaContext mediaContext,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory,
    IMediaAnalyzer mediaAnalyzer,
    ILogger<LibraryManager> logger,
    IEventBus? eventBus = null
) : BaseManager, ILibraryManager
{
    private Library? _library;

    public async Task ProcessLibrary(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id: id);
        if (_library is null)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsFound = 0;

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null)
        {
            await bus.PublishAsync(
                @event: new LibraryScanStartedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                }
            );
        }

        // Pass the whole Folder (DriverId + Path) so the scan can hit the
        // right backend (NFS / S3 / WebDAV / local). Flattening to a bare
        // string path was making every scan run against the local injected
        // driver — remote folders returned 0 results silently.
        List<Folder> targets =
        [
            .. _library.FolderLibraries.Select(selector: folderLibrary => folderLibrary.Folder),
        ];

        int depth = GetDepth();

        await Parallel.ForEachAsync(
            source: targets,
            parallelOptions: SystemParallelism.Options,
            body: async (folder, _) =>
            {
                logger.LogInformation(message: "Scanning {Path}", args: folder.Path);
                switch (_library.Type)
                {
                    case MediaTypes.MusicMediaType:
                        int audioCount = await ScanAudioFolder(folder: folder, depth: depth);
                        Interlocked.Add(location1: ref itemsFound, value: audioCount);
                        break;
                    case MediaTypes.AnimeMediaType:
                    case MediaTypes.TvMediaType:
                    case MediaTypes.MovieMediaType:
                        int videoCount = await ScanVideoFolder(folder: folder, depth: depth);
                        Interlocked.Add(location1: ref itemsFound, value: videoCount);
                        break;
                }
            }
        );

        stopwatch.Stop();

        if (bus is not null)
        {
            await bus.PublishAsync(
                @event: new LibraryScanCompletedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                    ItemsFound = itemsFound,
                    Duration = stopwatch.Elapsed,
                }
            );
        }

        logger.LogInformation(message: "Scanning done");
    }

    public async Task ProcessNewLibraryItems(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id: id);
        if (_library is null)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsFound = 0;

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null)
        {
            await bus.PublishAsync(
                @event: new LibraryScanStartedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                }
            );
        }

        HashSet<string> existingFolders = await libraryRepository.GetExistingFolderNamesAsync(
            libraryId: id,
            libraryType: _library.Type
        );

        // See comment in ProcessLibrary — pass the Folder so the scan
        // resolves the right driver per folder.
        List<Folder> targets =
        [
            .. _library.FolderLibraries.Select(selector: folderLibrary => folderLibrary.Folder),
        ];

        int depth = GetDepth();

        await Parallel.ForEachAsync(
            source: targets,
            parallelOptions: SystemParallelism.Options,
            body: async (folder, _) =>
            {
                logger.LogInformation(message: "Scanning for new items in {Path}", args: folder.Path);
                switch (_library.Type)
                {
                    case MediaTypes.MusicMediaType:
                        int audioCount = await ScanNewAudioFolder(folder: folder, depth: depth, existingFolders: existingFolders);
                        Interlocked.Add(location1: ref itemsFound, value: audioCount);
                        break;
                    case MediaTypes.AnimeMediaType:
                    case MediaTypes.TvMediaType:
                    case MediaTypes.MovieMediaType:
                        int videoCount = await ScanNewVideoFolder(folder: folder, depth: depth, existingFolders: existingFolders);
                        Interlocked.Add(location1: ref itemsFound, value: videoCount);
                        break;
                }
            }
        );

        stopwatch.Stop();

        if (bus is not null)
        {
            await bus.PublishAsync(
                @event: new LibraryScanCompletedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                    ItemsFound = itemsFound,
                    Duration = stopwatch.Elapsed,
                }
            );
        }

        logger.LogInformation(message: "Scan for new items done — {ItemsFound} new items found", args: itemsFound);
    }

    // The IStorage facade's GetFullPath joins the configured backend root:
    // LocalStorage resolves the relative folder path against its rootPath guard,
    // producing the real absolute scan path. Remote backends (NFS/S3/WebDAV) do
    // not implement the facade escape-hatch and throw NotSupportedException; for
    // those the low-level driver already embeds its export/prefix, so resolve
    // through it. Using the driver for LOCAL would canonicalize against the
    // process CWD (the local driver is a shared, root-less singleton) and scan a
    // path that does not exist — the "0 subfolders" discovery bug.
    internal static string ResolveScanRoot(IStorage storage, string folderPath)
    {
        try
        {
            return storage.GetFullPath(path: folderPath);
        }
        catch (NotSupportedException)
        {
            return storage.Driver.GetFullPath(path: folderPath);
        }
    }

    private async Task<int> ScanNewVideoFolder(
        Folder folder,
        int depth,
        HashSet<string> existingFolders
    )
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        await using MediaScan mediaScan = new(driver: folderStorage.Driver);
        // Resolve the scan root through the facade so a LOCAL folder joins its
        // configured driver rootPath. The local driver is a shared, root-less
        // singleton (the root lives in the facade's guard), so driver.GetFullPath
        // canonicalizes a relative folder path against the process CWD -> a path
        // that does not exist -> "0 subfolders". ResolveScanRoot falls back to the
        // driver only for remote backends, whose driver embeds its own export.
        string scanRoot = ResolveScanRoot(storage: folderStorage, folderPath: folder.Path);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan.Process(rootFolder: scanRoot, depth: depth);

        List<MediaFolderExtend> newFolders = rootFolders
            .Where(predicate: f => !existingFolders.Contains(item: f.Name.NormalizeForComparison()))
            .ToList();

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in newFolders)
            {
                await bus.PublishAsync(
                    @event: new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        await Parallel.ForEachAsync(
            source: newFolders.OrderBy(keySelector: f => f.Path),
            parallelOptions: SystemParallelism.Options,
            body: async (rootFolder, _) =>
            {
                await ProcessVideoFolder(path: rootFolder);
            }
        );

        logger.LogInformation(
            message: "Found {Count} new subfolders (skipped {Count2} existing)", args: [newFolders.Count, rootFolders.Count - newFolders.Count]
        );
        return newFolders.Count;
    }

    private async Task<int> ScanNewAudioFolder(
        Folder folder,
        int depth,
        HashSet<string> existingFolders
    )
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        await using MediaScan mediaScan = new(driver: folderStorage.Driver);
        // Resolve the scan root through the facade so a LOCAL folder joins its
        // configured driver rootPath. The local driver is a shared, root-less
        // singleton (the root lives in the facade's guard), so driver.GetFullPath
        // canonicalizes a relative folder path against the process CWD -> a path
        // that does not exist -> "0 subfolders". ResolveScanRoot falls back to the
        // driver only for remote backends, whose driver embeds its own export.
        string scanRoot = ResolveScanRoot(storage: folderStorage, folderPath: folder.Path);
        List<MediaFolderExtend> rootFolders = (
            await mediaScan.DisableRegexFilter().Process(rootFolder: scanRoot, depth: depth)
        )
            .SelectMany(selector: r => r.SubFolders ?? [])
            .ToList();

        List<MediaFolderExtend> newFolders = rootFolders
            .Where(predicate: f => !existingFolders.Contains(item: f.Name.NormalizeForComparison()))
            .ToList();

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in newFolders)
            {
                await bus.PublishAsync(
                    @event: new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        Parallel.ForEach(
            source: newFolders.OrderBy(keySelector: f => f.Path),
            parallelOptions: SystemParallelism.Options,
            body: (rootFolder, _) =>
            {
                ProcessMusicFolder(baseFolderExtend: rootFolder);
            }
        );

        logger.LogInformation(
            message: "Found {Count} new subfolders (skipped {Count2} existing)", args: [newFolders.Count, rootFolders.Count - newFolders.Count]
        );
        return newFolders.Count;
    }

    private async Task<int> ScanVideoFolder(Folder folder, int depth)
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        await using MediaScan mediaScan = new(driver: folderStorage.Driver);
        // Resolve the scan root through the facade so a LOCAL folder joins its
        // configured driver rootPath. The local driver is a shared, root-less
        // singleton (the root lives in the facade's guard), so driver.GetFullPath
        // canonicalizes a relative folder path against the process CWD -> a path
        // that does not exist -> "0 subfolders". ResolveScanRoot falls back to the
        // driver only for remote backends, whose driver embeds its own export.
        string scanRoot = ResolveScanRoot(storage: folderStorage, folderPath: folder.Path);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan.Process(rootFolder: scanRoot, depth: depth);

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in rootFolders)
            {
                await bus.PublishAsync(
                    @event: new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        await Parallel.ForEachAsync(
            source: rootFolders.OrderBy(keySelector: f => f.Path),
            parallelOptions: SystemParallelism.Options,
            body: async (rootFolder, _) =>
            {
                await ProcessVideoFolder(path: rootFolder);
            }
        );

        logger.LogInformation(message: "Found {Count} subfolders", args: rootFolders.Count);
        return rootFolders.Count;
    }

    private async Task<int> ScanAudioFolder(Folder folder, int depth)
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        await using MediaScan mediaScan = new(driver: folderStorage.Driver);
        // Resolve the scan root through the facade so a LOCAL folder joins its
        // configured driver rootPath. The local driver is a shared, root-less
        // singleton (the root lives in the facade's guard), so driver.GetFullPath
        // canonicalizes a relative folder path against the process CWD -> a path
        // that does not exist -> "0 subfolders". ResolveScanRoot falls back to the
        // driver only for remote backends, whose driver embeds its own export.
        string scanRoot = ResolveScanRoot(storage: folderStorage, folderPath: folder.Path);
        List<MediaFolderExtend> rootFolders = (
            await mediaScan.DisableRegexFilter().Process(rootFolder: scanRoot, depth: depth)
        )
            .SelectMany(selector: r => r.SubFolders ?? [])
            .ToList();

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in rootFolders)
            {
                await bus.PublishAsync(
                    @event: new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        Parallel.ForEach(
            source: rootFolders.OrderBy(keySelector: f => f.Path),
            parallelOptions: SystemParallelism.Options,
            body: (rootFolder, _) =>
            {
                ProcessMusicFolder(baseFolderExtend: rootFolder);
            }
        );

        logger.LogInformation(message: "Found {Count} subfolders", args: rootFolders.Count);
        return rootFolders.Count;
    }

    private async Task ProcessVideoFolder(MediaFolderExtend path)
    {
        if (_library is null)
            return;
        switch (_library.Type)
        {
            case MediaTypes.MovieMediaType:
            {
                await ProcessMovieFolder(folderExtend: path);
                break;
            }
            case MediaTypes.AnimeMediaType:
            case MediaTypes.TvMediaType:
            {
                await ProcessTvFolder(folderExtend: path);
                break;
            }
        }
    }

    private async Task ProcessMovieFolder(MediaFolderExtend folderExtend)
    {
        if (_library is null)
            return;

        logger.LogInformation(message: "Processing movie folder {Path}", args: folderExtend.Path);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? paginatedMovieResponse = await tmdbSearchClient.Movie(
            query: folderExtend.Parsed.Title!,
            year: folderExtend.Parsed.Year
        );

        if (paginatedMovieResponse?.Results.Count <= 0)
            return;

        // List<Movie> res = FuzzyMatcher.SortByMatchPercentage(paginatedMovieResponse?.Results, m => m.Title, folder.Parsed.Title);
        IEnumerable<TmdbMovie> res = paginatedMovieResponse?.Results ?? [];
        if (res.Count() is 0)
            return;

        jobDispatcher.DispatchJob<MovieImportJob>(id: res.First().Id, library: _library);
    }

    private async Task ProcessTvFolder(MediaFolderExtend folderExtend)
    {
        if (_library is null)
            return;

        logger.LogInformation(message: "Processing tv folder {Path}", args: folderExtend.Path);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? paginatedTvShowResponse = await tmdbSearchClient.TvShow(
            query: folderExtend.Parsed.Title!,
            year: folderExtend.Parsed.Year
        );

        if (paginatedTvShowResponse?.Results.Count <= 0)
            return;

        // List<TvShow> res = FuzzyMatcher.SortByMatchPercentage(paginatedTvShowResponse.Results, m => m.Name, folder.Parsed.Title);
        IEnumerable<TmdbTvShow> res = paginatedTvShowResponse?.Results ?? [];
        if (!res.Any())
            return;

        jobDispatcher.DispatchJob<ShowImportJob>(id: res.First().Id, library: _library);
    }

    private void ProcessMusicFolder(MediaFolderExtend baseFolderExtend)
    {
        if (_library is null)
            return;

        jobDispatcher.DispatchJob<ReleaseImportJob>(baseFolderPath: baseFolderExtend.Path, libraryId: _library.Id);
    }

    private int GetDepth()
    {
        if (_library is null)
            return 0;

        return _library.Type switch
        {
            MediaTypes.MovieMediaType or MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => 1,
            MediaTypes.MusicMediaType => 2,
            _ => 1,
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

    public async Task<Library?> RescanFiles(Ulid libraryId, int id)
    {
        Library? library = await libraryRepository.GetLibraryByIdWithFolders(libraryId: libraryId);
        if (library is null)
        {
            logger.LogWarning(message: "Library with ID {LibraryId} not found", args: libraryId);
            return null;
        }

        FileRepository fileRepository = new(context: mediaContext, storageDriver: storageDriver);
        FileManager fileManager = new(fileRepository: fileRepository, storageFactory: storageFactory, storageDriver: storageDriver, mediaAnalyzer: mediaAnalyzer);

        _ = await fileManager.FindFiles(id: id, library: library);

        return library;
    }
}
