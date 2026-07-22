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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;
using NoMercyQueue;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class MusicEncodeJob : AbstractMusicEncoderJob, IJobStorageInjector
{
    private IEncodingOrchestrator? _encodingOrchestrator;

    public new void InjectStorageServices(IServiceProvider serviceProvider)
    {
        base.InjectStorageServices(serviceProvider: serviceProvider);
        _encodingOrchestrator = serviceProvider.GetRequiredService<IEncodingOrchestrator>();
    }

    public override string QueueName => "encoder";
    public override int Priority => 3;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        await using MediaContext context = new();

        await using LibraryRepository libraryRepository = new(context: context, storageDriver: StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(folderId: FolderId);
        if (folder is null)
            return;

        List<EncodingPreset> presets = folder
            .EncodingPresetFolders.Where(predicate: link => link.Preset is not null)
            .Select(selector: link => link.Preset!)
            .ToList();

        foreach (EncodingPreset preset in presets)
        {
            Track track = new()
            {
                Id = FoundTrack.Id,
                Name = FoundTrack.Title,
                FolderId = folder.Id,
                TrackNumber = FoundTrack.Position,
            };

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                EncodingProfile encodingProfile;
                try
                {
                    encodingProfile = PresetResolver.Resolve(
                        presetId: preset.Id,
                        lookup: new DbPresetLookup(context: context)
                    );
                }
                catch (Exception ex)
                {
                    Log.LogWarning(
                        message: "Skipping preset '{Name}' ({Id}): resolve failed — {Message}", args: [preset.Name, preset.Id, ex.Message]
                    );
                    continue;
                }

                if (encodingProfile.Audio.Length == 0)
                {
                    Log.LogInformation(
                        message: "Skipping preset {Name}: no audio outputs configured",
                        args: preset.Name
                    );
                    continue;
                }

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new EncodingStartedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            InputPath = MediaFile.Path,
                            OutputPath = FolderMetaData.BasePath,
                            ProfileName = preset.Name,
                        }
                    );
                }

                IEncodingOrchestrator orchestrator = _encodingOrchestrator!;

                IStorage folderStorage = StorageFactory.For(
                    folderId: folder.Id,
                    driverId: folder.DriverId,
                    subPath: folder.Path
                );

                EncodingRequest request = new(
                    InputPath: MediaFile.Path,
                    OutputDirectory: FolderMetaData.BasePath,
                    Profile: encodingProfile,
                    SourceStorage: folderStorage,
                    DestinationStorage: folderStorage
                );

                EventBusProgressObserver progressObserver = new(
                    jobId: track.Id.GetHashCode(),
                    title: FoundTrack.Title
                );

                EncodingResult encodeResult = await orchestrator.EncodeAsync(
                    request: request,
                    progress: progressObserver
                );

                if (!encodeResult.Success)
                {
                    throw new InvalidOperationException(
                        message: $"Encoding failed for {MediaFile.Path}: {encodeResult.Error?.Message ?? "unknown error"}"
                    );
                }

                Log.LogInformation(
                    message: "Encoded {Path} → {OutputPath} in {TotalSeconds:F1}s ({Unknown})", args: [MediaFile.Path, encodeResult.OutputPath, encodeResult.Duration.TotalSeconds, encodeResult.Metrics?.EncoderUsed ?? "unknown"]
                );

                await AddRecording(folder: folder);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new EncodingStageChangedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            Status = "completed",
                            Title = FoundTrack.Title,
                            Message = "Done",
                        }
                    );

                    stopwatch.Stop();
                    await EventBusProvider.Current.PublishAsync(
                        @event: new EncodingCompletedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            OutputPath = FolderMetaData.BasePath,
                            Duration = stopwatch.Elapsed,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.LogError(exception: e, message: "Music encode task failed");

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        @event: new EncodingStageChangedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            Status = "failed",
                            Title = FoundTrack.Title,
                            Message = e.Message,
                        }
                    );

                    await EventBusProvider.Current.PublishAsync(
                        @event: new EncodingFailedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            InputPath = MediaFile.Path,
                            ErrorMessage = e.Message,
                            ExceptionType = e.GetType().Name,
                        }
                    );
                }

                // Re-throw so the queue system marks this job as failed and can retry it.
                throw;
            }
        }
    }

    private async Task AddRecording(Folder folder)
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MusicGenreRepository musicGenreRepository = new(context: context);

        ArtistRepository artistRepository = new(context: context);
        ArtistManager artistManager = new(
            artistRepository: artistRepository,
            musicGenreRepository: musicGenreRepository,
            jobDispatcher: jobDispatcher,
            storageFactory: StorageFactory,
            logger: LoggerFactory.CreateLogger<ArtistManager>()
        );

        RecordingRepository recordingRepository = new(context: context);
        RecordingManager recordingManager = new(
            recordingRepository: recordingRepository,
            musicGenreRepository: musicGenreRepository,
            artistRepository: artistRepository,
            storageDriver: StorageDriver,
            storageFactory: StorageFactory,
            logger: LoggerFactory.CreateLogger<RecordingManager>()
        );

        await using MediaScan mediaScan = new(driver: StorageDriver);

        // V3 encoder writes to BasePath — scan picks up all encoded output in that folder
        MediaFolderExtend mediaFolder = (
            await mediaScan
                .EnableFileListing()
                .FilterByMediaType(mediaType: "music")
                .Process(rootFolder: FolderMetaData.BasePath)
        ).First();

        CoverArtImageManagerManager.CoverPalette? coverPalette =
            await CoverArtImageManagerManager.Add(
                id: FolderMetaData.MusicBrainzRelease.MusicBrainzReleaseGroup.Id
            );

        await Parallel.ForEachAsync(
            source: FolderMetaData.MusicBrainzRelease.Media,
            parallelOptions: SystemParallelism.Options,
            body: async (media, t) =>
            {
                if (
                    !await recordingManager.Store(
                        releaseAppends: FolderMetaData.MusicBrainzRelease,
                        musicBrainzTrack: FoundTrack,
                        musicBrainzMedia: media,
                        libraryFolder: folder,
                        mediaFolder: mediaFolder,
                        releaseCoverPalette: coverPalette
                    )
                )
                    return;

                Library? albumLibrary = folder
                    .FolderLibraries.FirstOrDefault(predicate: f => f.LibraryId == LibraryId)
                    ?.Library;

                if (albumLibrary is null)
                {
                    Log.LogError(message: "Album Library not found: {LibraryId}", args: LibraryId);
                    return;
                }

                await Parallel.ForEachAsync(
                    source: FoundTrack.ArtistCredit,
                    parallelOptions: SystemParallelism.Options,
                    body: async (artist, _) =>
                    {
                        Log.LogTrace(message: "Storing Artist: {Name}", args: artist.MusicBrainzArtist.Name);
                        await artistManager.Store(
                            artistCredit: artist.MusicBrainzArtist,
                            library: albumLibrary,
                            libraryFolder: folder,
                            mediaFolder: mediaFolder,
                            track: FoundTrack
                        );

                        jobDispatcher.DispatchJob<MusicMetadataJob>(musicBrainzArtist: artist.MusicBrainzArtist);
                    }
                );
            }
        );
    }
}
