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
        base.InjectStorageServices(serviceProvider);
        _encodingOrchestrator = serviceProvider.GetRequiredService<IEncodingOrchestrator>();
    }

    public override string QueueName => "encoder";

    // The queue reserves by OrderByDescending(Priority), so 3 sat below VideoEncodeJob's
    // 4 and an album import never got a worker while any video backlog was outstanding —
    // on a library mid-encode that is days, and the operator who just picked a release
    // sees nothing appear.
    //
    // Outranking the video coordinator costs it nothing: EncodeTaskJob does the actual
    // ffmpeg work on encoder-gpu/encoder-cpu, so the only thing yielding here is an
    // analyze-then-WaitChildren step.
    public override int Priority => 5;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        // The release this encode belongs to is stored once and shared by every
        // track of the album, so it is read back here rather than carried in the
        // payload. A release that is gone belonged to work already finished.
        if (!await Hydrate())
        {
            Log.LogWarning(
                "Skipping encode for track {TrackId}: release {ReleaseId} is no longer stored",
                TrackId,
                ReleaseId
            );
            return;
        }

        await using MediaContext context = new();

        await using LibraryRepository libraryRepository = new(context, StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        List<EncodingPreset> presets = folder
            .EncodingPresetFolders.Where(link => link.Preset is not null)
            .Select(link => link.Preset!)
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
                        preset.Id,
                        new DbPresetLookup(context)
                    );
                }
                catch (Exception ex)
                {
                    Log.LogWarning(
                        "Skipping preset '{Name}' ({Id}): resolve failed — {Message}",
                        preset.Name,
                        preset.Id,
                        ex.Message
                    );
                    continue;
                }

                if (encodingProfile.Audio.Length == 0)
                {
                    Log.LogInformation(
                        "Skipping preset {Name}: no audio outputs configured",
                        preset.Name
                    );
                    continue;
                }

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStartedEvent
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
                    folder.Id,
                    folder.DriverId,
                    folder.Path
                );

                EncodingRequest request = new(
                    InputPath: MediaFile.Path,
                    OutputDirectory: FolderMetaData.BasePath,
                    Profile: encodingProfile,
                    SourceStorage: folderStorage,
                    DestinationStorage: folderStorage
                );

                EventBusProgressObserver progressObserver = new(
                    track.Id.GetHashCode(),
                    FoundTrack.Title
                );

                EncodingResult encodeResult = await orchestrator.EncodeAsync(
                    request,
                    progressObserver
                );

                if (!encodeResult.Success)
                {
                    throw new InvalidOperationException(
                        $"Encoding failed for {MediaFile.Path}: {encodeResult.Error?.Message ?? "unknown error"}"
                    );
                }

                Log.LogInformation(
                    "Encoded {Path} → {OutputPath} in {TotalSeconds:F1}s ({Unknown})",
                    MediaFile.Path,
                    encodeResult.OutputPath,
                    encodeResult.Duration.TotalSeconds,
                    encodeResult.Metrics?.EncoderUsed ?? "unknown"
                );

                await AddRecording(folder);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            Status = "completed",
                            Title = FoundTrack.Title,
                            Message = "Done",
                        }
                    );

                    stopwatch.Stop();
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingCompletedEvent
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
                Log.LogError(e, "Music encode task failed");

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = track.Id.GetHashCode(),
                            Status = "failed",
                            Title = FoundTrack.Title,
                            Message = e.Message,
                        }
                    );

                    await EventBusProvider.Current.PublishAsync(
                        new EncodingFailedEvent
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

        MusicGenreRepository musicGenreRepository = new(context);

        ArtistRepository artistRepository = new(context);
        ArtistManager artistManager = new(
            artistRepository,
            musicGenreRepository,
            jobDispatcher,
            StorageFactory,
            LoggerFactory.CreateLogger<ArtistManager>()
        );

        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(
            recordingRepository,
            musicGenreRepository,
            artistRepository,
            StorageDriver,
            StorageFactory,
            LoggerFactory.CreateLogger<RecordingManager>()
        );

        await using MediaScan mediaScan = new(StorageDriver);

        // V3 encoder writes to BasePath — scan picks up all encoded output in that folder
        MediaFolderExtend mediaFolder = (
            await mediaScan
                .EnableFileListing()
                .FilterByMediaType("music")
                .Process(FolderMetaData.BasePath)
        ).First();

        CoverArtImageManagerManager.CoverPalette? coverPalette =
            await CoverArtImageManagerManager.Add(
                FolderMetaData.MusicBrainzRelease.MusicBrainzReleaseGroup.Id
            );

        await Parallel.ForEachAsync(
            FolderMetaData.MusicBrainzRelease.Media,
            SystemParallelism.Options,
            async (media, t) =>
            {
                if (
                    !await recordingManager.Store(
                        FolderMetaData.MusicBrainzRelease,
                        FoundTrack,
                        media,
                        folder,
                        mediaFolder,
                        coverPalette
                    )
                )
                    return;

                Library? albumLibrary = folder
                    .FolderLibraries.FirstOrDefault(f => f.LibraryId == LibraryId)
                    ?.Library;

                if (albumLibrary is null)
                {
                    Log.LogError("Album Library not found: {LibraryId}", LibraryId);
                    return;
                }

                await Parallel.ForEachAsync(
                    FoundTrack.ArtistCredit,
                    SystemParallelism.Options,
                    async (artist, _) =>
                    {
                        Log.LogTrace("Storing Artist: {Name}", artist.MusicBrainzArtist.Name);
                        await artistManager.Store(
                            artist.MusicBrainzArtist,
                            albumLibrary,
                            folder,
                            mediaFolder,
                            FoundTrack
                        );

                        jobDispatcher.DispatchJob<MusicMetadataJob>(artist.MusicBrainzArtist);
                    }
                );
            }
        );
    }
}
