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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.EventHandlers;

public class FileWatcherEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly SemaphoreSlim _semaphore = new(initialCount: 2);
    private readonly IStorageDriver _storageDriver;
    private readonly IStorageFactory _storageFactory;

    private readonly ILogger<FileWatcherEventHandler> _logger;

    public FileWatcherEventHandler(
        ILogger<FileWatcherEventHandler> logger,
        IEventBus eventBus,
        IStorageDriver storageDriver,
        IStorageFactory storageFactory
    )
    {
        _logger = logger;
        _storageDriver = storageDriver;
        _storageFactory = storageFactory;
        _subscriptions.Add(item: eventBus.Subscribe<FileCreatedEvent>(handler: OnFileCreated));
        _subscriptions.Add(item: eventBus.Subscribe<FileDeletedEvent>(handler: OnFileDeleted));
        _subscriptions.Add(item: eventBus.Subscribe<FileRenamedEvent>(handler: OnFileRenamed));
    }

    internal async Task OnFileCreated(FileCreatedEvent @event, CancellationToken ct)
    {
        await _semaphore.WaitAsync(cancellationToken: ct);
        try
        {
            _logger.LogInformation(
                message: "FileWatcher: Processing new/changed content in {FolderPath}",
                args: @event.FolderPath
            );

            MediaScan mediaScan = new(driver: _storageDriver);
            MediaScan scan = mediaScan.EnableFileListing();

            if (@event.LibraryType == MediaTypes.MusicMediaType)
                scan.DisableRegexFilter();

            ConcurrentBag<MediaFolderExtend> mediaFolders = await scan.Process(rootFolder: @event.FolderPath);

            if (mediaFolders.Count == 0)
            {
                _logger.LogWarning(
                    message: "FileWatcher: No media found in {FolderPath}",
                    args: @event.FolderPath
                );
                return;
            }

            MediaFolderExtend mediaFolder = mediaFolders.First();

            switch (@event.LibraryType)
            {
                case MediaTypes.InboxMediaType:
                    return;
                case MediaTypes.MovieMediaType:
                    await HandleMovieFolder(@event: @event, mediaFolder: mediaFolder);
                    break;
                case MediaTypes.TvMediaType:
                case MediaTypes.AnimeMediaType:
                    await HandleTvFolder(@event: @event, mediaFolder: mediaFolder);
                    break;
                case MediaTypes.MusicMediaType:
                    HandleMusicFolder(@event: @event, mediaFolder: mediaFolder);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                message: "FileWatcher: Error processing {FolderPath}: {Message}", args: [@event.FolderPath, ex.Message]
            );
        }
        finally
        {
            _semaphore.Release();
        }
    }

    internal async Task OnFileDeleted(FileDeletedEvent @event, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                message: "FileWatcher: Processing deletion of {FullPath}",
                args: @event.FullPath
            );

            string hostFolder = Path.GetDirectoryName(path: @event.FullPath).OrEmpty();
            string filename = "/" + Path.GetFileName(path: @event.FullPath);

            await using MediaContext mediaContext = new();
            FileRepository fileRepository = new(context: mediaContext, storageDriver: _storageDriver);

            int videoFilesDeleted = await fileRepository.DeleteVideoFilesByHostFolderAsync(
                hostFolder: hostFolder
            );
            int metadataDeleted = await fileRepository.DeleteMetadataByHostFolderAsync(hostFolder: hostFolder);

            _logger.LogInformation(
                message: "FileWatcher: Deleted {VideoFilesDeleted} video file(s) and {MetadataDeleted} metadata record(s) for {HostFolder}", args: [videoFilesDeleted, metadataDeleted, hostFolder]
            );

            if (videoFilesDeleted > 0 && EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(
                    @event: new LibraryRefreshedEvent { QueryKey = ["base", "libraries"] }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                message: "FileWatcher: Error processing deletion of {FullPath}: {Message}", args: [@event.FullPath, ex.Message]
            );
        }
    }

    internal async Task OnFileRenamed(FileRenamedEvent @event, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                message: "FileWatcher: Processing rename from {OldFullPath} to {NewFullPath}", args: [@event.OldFullPath, @event.NewFullPath]
            );

            string oldHostFolder = Path.GetDirectoryName(path: @event.OldFullPath).OrEmpty();
            string oldFilename = "/" + Path.GetFileName(path: @event.OldFullPath);
            string newHostFolder = Path.GetDirectoryName(path: @event.NewFullPath).OrEmpty();
            string newFilename = "/" + Path.GetFileName(path: @event.NewFullPath);

            await using MediaContext mediaContext = new();
            FileRepository fileRepository = new(context: mediaContext, storageDriver: _storageDriver);

            int updated = await fileRepository.UpdateVideoFilePathsAsync(
                oldHostFolder: oldHostFolder,
                oldFilename: oldFilename,
                newHostFolder: newHostFolder,
                newFilename: newFilename
            );

            if (updated > 0)
            {
                _logger.LogInformation(
                    message: "FileWatcher: Updated {Updated} video file path(s) from {OldHostFolder} to {NewHostFolder}", args: [updated, oldHostFolder, newHostFolder]
                );

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new LibraryRefreshedEvent { QueryKey = ["base", "libraries"] }
                    );
                }
            }
            else
            {
                _logger.LogDebug(
                    message: "FileWatcher: No matching records found for rename, treating as new content"
                );
                await OnFileCreated(
                    @event: new()
                    {
                        FolderPath = newHostFolder,
                        LibraryId = @event.LibraryId,
                        LibraryType = @event.LibraryType,
                    },
                    ct: ct
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                message: "FileWatcher: Error processing rename from {OldFullPath} to {NewFullPath}: {Message}", args: [@event.OldFullPath, @event.NewFullPath, ex.Message]
            );
        }
    }

    private async Task HandleMovieFolder(FileCreatedEvent @event, MediaFolderExtend mediaFolder)
    {
        if (mediaFolder.Parsed.Title is null)
        {
            _logger.LogWarning(message: "FileWatcher: Could not parse title from {Path}", args: mediaFolder.Path);
            return;
        }

        _logger.LogInformation(
            message: "FileWatcher: Movie {Path}: Searching TMDB for '{Title}'", args: [mediaFolder.Path, mediaFolder.Parsed.Title]
        );

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? response = await tmdbSearchClient.Movie(
            query: mediaFolder.Parsed.Title,
            year: mediaFolder.Parsed.Year
        );

        if (response?.Results is null || response.Results.Count == 0)
        {
            _logger.LogWarning(
                message: "FileWatcher: No TMDB results found for movie '{Title}'",
                args: mediaFolder.Parsed.Title
            );
            return;
        }

        TmdbMovie? movie = response.Results.MaxBy(keySelector: result =>
            ScoreCandidate(
                candidateTitle: result.Title,
                candidateDate: result.ReleaseDate,
                parsedTitle: mediaFolder.Parsed.Title,
                parsedYear: mediaFolder.Parsed.Year
            )
        );
        if (
            movie is null
            || FuzzyMatcher.MatchPercentage(strA: movie.Title, strB: mediaFolder.Parsed.Title)
                < MinMatchConfidence
        )
        {
            _logger.LogWarning(
                message: "FileWatcher: No confident TMDB match for movie '{Title}'",
                args: mediaFolder.Parsed.Title
            );
            return;
        }

        _logger.LogInformation(
            message: "FileWatcher: Movie '{Title}' found on TMDB (ID: {Id}), dispatching job", args: [movie.Title, movie.Id]
        );

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<MovieImportJob>(id: movie.Id, libraryId: @event.LibraryId);
    }

    private async Task HandleTvFolder(FileCreatedEvent @event, MediaFolderExtend mediaFolder)
    {
        if (mediaFolder.Parsed.Title is null)
        {
            _logger.LogWarning(message: "FileWatcher: Could not parse title from {Path}", args: mediaFolder.Path);
            return;
        }

        _logger.LogInformation(
            message: "FileWatcher: TV Show {Path}: Searching TMDB for '{Title}'", args: [mediaFolder.Path, mediaFolder.Parsed.Title]
        );

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? response = await tmdbSearchClient.TvShow(
            query: mediaFolder.Parsed.Title,
            year: mediaFolder.Parsed.Year
        );

        if (response?.Results is null || response.Results.Count == 0)
        {
            _logger.LogWarning(
                message: "FileWatcher: No TMDB results found for TV show '{Title}'",
                args: mediaFolder.Parsed.Title
            );
            return;
        }

        TmdbTvShow? show = response.Results.MaxBy(keySelector: result =>
            ScoreCandidate(
                candidateTitle: result.Name,
                candidateDate: result.FirstAirDate,
                parsedTitle: mediaFolder.Parsed.Title,
                parsedYear: mediaFolder.Parsed.Year
            )
        );
        if (
            show is null
            || FuzzyMatcher.MatchPercentage(strA: show.Name, strB: mediaFolder.Parsed.Title)
                < MinMatchConfidence
        )
        {
            _logger.LogWarning(
                message: "FileWatcher: No confident TMDB match for TV show '{Title}'",
                args: mediaFolder.Parsed.Title
            );
            return;
        }

        _logger.LogInformation(
            message: "FileWatcher: TV Show '{Name}' found on TMDB (ID: {Id}), dispatching job", args: [show.Name, show.Id]
        );

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<ShowImportJob>(id: show.Id, libraryId: @event.LibraryId);
    }

    private void HandleMusicFolder(FileCreatedEvent @event, MediaFolderExtend mediaFolder)
    {
        _logger.LogInformation(message: "FileWatcher: Music {Path}: Processing", args: mediaFolder.Path);

        string directoryPath = Path.GetFullPath(path: mediaFolder.Path);

        using MediaContext mediaContext = new();
        Library? library = mediaContext
            .Libraries.Include(navigationPropertyPath: l => l.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
            .FirstOrDefault(predicate: l => l.Id == @event.LibraryId);

        if (library is null)
            return;

        FolderLibrary? folderLibrary = library.FolderLibraries.FirstOrDefault(predicate: f =>
        {
            // Resolve through the driver, not the IStorage facade: the facade's
            // GetFullPath is a LocalStorage-only escape hatch that throws on
            // every remote backend, so a facade call here killed folder
            // matching for NFS / SMB music libraries.
            string driverRoot = _storageFactory
                .For(folderId: f.Folder.Id, driverId: f.Folder.DriverId, subPath: string.Empty)
                .Driver.GetFullPath(path: f.Folder.Path);
            return directoryPath.StartsWith(value: driverRoot, comparisonType: StringComparison.OrdinalIgnoreCase);
        });
        if (folderLibrary is null)
            return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AudioImportJob>(
            libraryId: @event.LibraryId,
            folderId: folderLibrary.FolderId,
            filePath: directoryPath
        );
    }

    private const double MinMatchConfidence = 50.0;

    private static double ScoreCandidate(
        string candidateTitle,
        DateTime? candidateDate,
        string parsedTitle,
        string? parsedYear
    )
    {
        double score = FuzzyMatcher.MatchPercentage(strA: candidateTitle, strB: parsedTitle);
        if (int.TryParse(s: parsedYear, result: out int year) && candidateDate?.Year == year)
            score += 25;
        return score;
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
        _semaphore.Dispose();
    }
}
