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
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Libraries;

public class LibraryManager(
    LibraryRepository libraryRepository,
    JobDispatcher jobDispatcher,
    MediaContext mediaContext,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory,
    IEventBus? eventBus = null
) : BaseManager, ILibraryManager
{
    private Library? _library;

    public async Task ProcessLibrary(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id);
        if (_library is null)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsFound = 0;

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null)
        {
            await bus.PublishAsync(
                new LibraryScanStartedEvent
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
            .. _library.FolderLibraries.Select(folderLibrary => folderLibrary.Folder),
        ];

        int depth = GetDepth();

        await Parallel.ForEachAsync(
            targets,
            SystemParallelism.Options,
            async (folder, _) =>
            {
                Logger.App("Scanning " + folder.Path);
                switch (_library.Type)
                {
                    case MediaTypes.MusicMediaType:
                        int audioCount = await ScanAudioFolder(folder, depth);
                        Interlocked.Add(ref itemsFound, audioCount);
                        break;
                    case MediaTypes.AnimeMediaType:
                    case MediaTypes.TvMediaType:
                    case MediaTypes.MovieMediaType:
                        int videoCount = await ScanVideoFolder(folder, depth);
                        Interlocked.Add(ref itemsFound, videoCount);
                        break;
                }
            }
        );

        stopwatch.Stop();

        if (bus is not null)
        {
            await bus.PublishAsync(
                new LibraryScanCompletedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                    ItemsFound = itemsFound,
                    Duration = stopwatch.Elapsed,
                }
            );
        }

        Logger.App("Scanning done");
    }

    public async Task ProcessNewLibraryItems(Ulid id)
    {
        _library = await libraryRepository.GetLibraryWithFolders(id);
        if (_library is null)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        int itemsFound = 0;

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null)
        {
            await bus.PublishAsync(
                new LibraryScanStartedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                }
            );
        }

        HashSet<string> existingFolders = await libraryRepository.GetExistingFolderNamesAsync(
            id,
            _library.Type
        );

        // See comment in ProcessLibrary — pass the Folder so the scan
        // resolves the right driver per folder.
        List<Folder> targets =
        [
            .. _library.FolderLibraries.Select(folderLibrary => folderLibrary.Folder),
        ];

        int depth = GetDepth();

        await Parallel.ForEachAsync(
            targets,
            SystemParallelism.Options,
            async (folder, _) =>
            {
                Logger.App("Scanning for new items in " + folder.Path);
                switch (_library.Type)
                {
                    case MediaTypes.MusicMediaType:
                        int audioCount = await ScanNewAudioFolder(folder, depth, existingFolders);
                        Interlocked.Add(ref itemsFound, audioCount);
                        break;
                    case MediaTypes.AnimeMediaType:
                    case MediaTypes.TvMediaType:
                    case MediaTypes.MovieMediaType:
                        int videoCount = await ScanNewVideoFolder(folder, depth, existingFolders);
                        Interlocked.Add(ref itemsFound, videoCount);
                        break;
                }
            }
        );

        stopwatch.Stop();

        if (bus is not null)
        {
            await bus.PublishAsync(
                new LibraryScanCompletedEvent
                {
                    LibraryId = _library.Id,
                    LibraryName = _library.Title,
                    ItemsFound = itemsFound,
                    Duration = stopwatch.Elapsed,
                }
            );
        }

        Logger.App($"Scan for new items done — {itemsFound} new items found");
    }

    private async Task<int> ScanNewVideoFolder(
        Folder folder,
        int depth,
        HashSet<string> existingFolders
    )
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        await using MediaScan mediaScan = new(folderStorage.Driver);
        string scanRoot = folderStorage.GetFullPath(folder.Path);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan.Process(scanRoot, depth);

        List<MediaFolderExtend> newFolders = rootFolders
            .Where(f => !existingFolders.Contains(f.Name.NormalizeForComparison()))
            .ToList();

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in newFolders)
            {
                await bus.PublishAsync(
                    new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        await Parallel.ForEachAsync(
            newFolders.OrderBy(f => f.Path),
            SystemParallelism.Options,
            async (rootFolder, _) =>
            {
                await ProcessVideoFolder(rootFolder);
            }
        );

        Logger.App(
            $"Found {newFolders.Count} new subfolders (skipped {rootFolders.Count - newFolders.Count} existing)"
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
        IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        await using MediaScan mediaScan = new(folderStorage.Driver);
        string scanRoot = folderStorage.GetFullPath(folder.Path);
        List<MediaFolderExtend> rootFolders = (
            await mediaScan.DisableRegexFilter().Process(scanRoot, depth)
        )
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        List<MediaFolderExtend> newFolders = rootFolders
            .Where(f => !existingFolders.Contains(f.Name.NormalizeForComparison()))
            .ToList();

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in newFolders)
            {
                await bus.PublishAsync(
                    new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        Parallel.ForEach(
            newFolders.OrderBy(f => f.Path),
            SystemParallelism.Options,
            (rootFolder, _) =>
            {
                ProcessMusicFolder(rootFolder);
            }
        );

        Logger.App(
            $"Found {newFolders.Count} new subfolders (skipped {rootFolders.Count - newFolders.Count} existing)"
        );
        return newFolders.Count;
    }

    private async Task<int> ScanVideoFolder(Folder folder, int depth)
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        await using MediaScan mediaScan = new(folderStorage.Driver);
        string scanRoot = folderStorage.GetFullPath(folder.Path);
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan.Process(scanRoot, depth);

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in rootFolders)
            {
                await bus.PublishAsync(
                    new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        await Parallel.ForEachAsync(
            rootFolders.OrderBy(f => f.Path),
            SystemParallelism.Options,
            async (rootFolder, _) =>
            {
                await ProcessVideoFolder(rootFolder);
            }
        );

        Logger.App("Found " + rootFolders.Count + " subfolders");
        return rootFolders.Count;
    }

    private async Task<int> ScanAudioFolder(Folder folder, int depth)
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        await using MediaScan mediaScan = new(folderStorage.Driver);
        string scanRoot = folderStorage.GetFullPath(folder.Path);
        List<MediaFolderExtend> rootFolders = (
            await mediaScan.DisableRegexFilter().Process(scanRoot, depth)
        )
            .SelectMany(r => r.SubFolders ?? [])
            .ToList();

        IEventBus? bus =
            eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);

        if (bus is not null && _library is not null)
        {
            foreach (MediaFolderExtend mediaFolder in rootFolders)
            {
                await bus.PublishAsync(
                    new MediaDiscoveredEvent
                    {
                        FilePath = mediaFolder.Path,
                        LibraryId = _library.Id,
                        DetectedType = _library.Type,
                    }
                );
            }
        }

        Parallel.ForEach(
            rootFolders.OrderBy(f => f.Path),
            SystemParallelism.Options,
            (rootFolder, _) =>
            {
                ProcessMusicFolder(rootFolder);
            }
        );

        Logger.App("Found " + rootFolders.Count + " subfolders");
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
                await ProcessMovieFolder(path);
                break;
            }
            case MediaTypes.AnimeMediaType:
            case MediaTypes.TvMediaType:
            {
                await ProcessTvFolder(path);
                break;
            }
        }
    }

    private async Task ProcessMovieFolder(MediaFolderExtend folderExtend)
    {
        if (_library is null)
            return;

        Logger.App("Processing movie folder " + folderExtend.Path);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? paginatedMovieResponse = await tmdbSearchClient.Movie(
            folderExtend.Parsed.Title!,
            folderExtend.Parsed.Year
        );

        if (paginatedMovieResponse?.Results.Count <= 0)
            return;

        // List<Movie> res = Str.SortByMatchPercentage(paginatedMovieResponse?.Results, m => m.Title, folder.Parsed.Title);
        IEnumerable<TmdbMovie> res = paginatedMovieResponse?.Results ?? [];
        if (res.Count() is 0)
            return;

        jobDispatcher.DispatchJob<MovieImportJob>(res.First().Id, _library);
    }

    private async Task ProcessTvFolder(MediaFolderExtend folderExtend)
    {
        if (_library is null)
            return;

        Logger.App("Processing tv folder " + folderExtend.Path);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? paginatedTvShowResponse = await tmdbSearchClient.TvShow(
            folderExtend.Parsed.Title!,
            folderExtend.Parsed.Year
        );

        if (paginatedTvShowResponse?.Results.Count <= 0)
            return;

        // List<TvShow> res = Str.SortByMatchPercentage(paginatedTvShowResponse.Results, m => m.Name, folder.Parsed.Title);
        IEnumerable<TmdbTvShow> res = paginatedTvShowResponse?.Results ?? [];
        if (!res.Any())
            return;

        jobDispatcher.DispatchJob<ShowImportJob>(res.First().Id, _library);
    }

    private void ProcessMusicFolder(MediaFolderExtend baseFolderExtend)
    {
        if (_library is null)
            return;

        jobDispatcher.DispatchJob<ReleaseImportJob>(baseFolderExtend.Path, _library.Id);
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
        Library? library = await libraryRepository.GetLibraryByIdWithFolders(libraryId);
        if (library is null)
        {
            Logger.App("Library with ID " + libraryId + " not found", LogEventLevel.Warning);
            return null;
        }

        FileRepository fileRepository = new(mediaContext, storageDriver);
        FileManager fileManager = new(fileRepository, storageFactory, storageDriver);

        await fileManager.FindFiles(id, library);

        return library;
    }
}
