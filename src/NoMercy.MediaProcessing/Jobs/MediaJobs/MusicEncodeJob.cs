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
using NoMercy.Storage.Validation;
using NoMercyQueue;
using NoMercyQueue.Core;
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

    // Runs the real ffmpeg encode inline (orchestrator.EncodeAsync, below) rather than
    // handing it to a child EncodeTaskJob the way VideoEncodeJob does. Queuing it on
    // 'encoder' therefore blocked that queue's single coordinator worker for the full
    // audio-encode duration per track — 16k+ queued tracks stalled every VideoEncodeJob's
    // orchestration step behind it, not just outranked it. encoder-cpu is where this
    // work actually belongs: the same lane EncodeTaskJob's CPU-side ffmpeg work runs on.
    public override string QueueName => QueueNames.EncoderCpu;

    // The queue reserves by OrderByDescending(Priority); kept above EncodeTaskJob's
    // usual footing so a freshly-imported album still surfaces promptly even while a
    // video backlog occupies encoder-cpu.
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
                    OutputDirectory: AlbumOutputDirectory(FolderMetaData.BasePath),
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

    /// <summary>
    /// The album folder the encoder writes into, relative to the destination
    /// storage root — which is what an OutputDirectory has to be.
    ///
    /// <para>Rows queued before the dispatcher was corrected carry a whole local
    /// path there, resolved against the running process rather than the library:
    /// the encoder rejects a rooted OutputDirectory outright, so every one of
    /// them failed without writing a byte, and there are thousands of them
    /// waiting. Reading the album folder back out repairs those in place rather
    /// than asking for a queue migration, and it costs a stored path nothing.</para>
    /// </summary>
    internal static string AlbumOutputDirectory(string basePath)
    {
        // ’ (U+2019) reproducibly fails to create a directory on Stoney's NAS — see
        // PicardNaming.FoldUnsafeUnicode. New dispatches never carry it, but a row
        // queued before that fix still does, so it is repaired here the same way the
        // rooted-path shape below is: on read, not by asking for a queue migration.
        string normalized = basePath.Replace('\\', '/').TrimEnd('/').Replace('’', '\'');
        if (normalized.Length == 0)
            return string.Empty;

        return StoragePathGuard.IsRootedAnyStyle(normalized)
            ? normalized[(normalized.LastIndexOf('/') + 1)..]
            : normalized;
    }

    private async Task AddRecording(Folder folder)
    {
        JobDispatcher jobDispatcher = new();

        await using MediaScan mediaScan = new(StorageDriver);

        // V3 encoder writes to BasePath — scan picks up all encoded output in that folder.
        //
        // BasePath is relative to the destination storage root, which is what the
        // encoder requires of an OutputDirectory. The scanner walks the filesystem
        // and needs the whole path, so it is rebuilt here from the folder the
        // library keeps rather than carried around absolute for one consumer's sake.
        // Asked through the storage the encoder wrote with, which is rooted at the
        // library folder on the folder's own driver. Asking a storage built with
        // no root resolves the library's relative path against the running
        // process instead of the driver's configured root, so the scan looked
        // inside the application directory and found an empty folder.
        string albumPath = StorageFactory
            .For(folder.Id, folder.DriverId, folder.Path)
            .GetFullPath(AlbumOutputDirectory(FolderMetaData.BasePath))
            .Replace('\\', '/');

        MediaFolderExtend mediaFolder = (
            await mediaScan.EnableFileListing().FilterByMediaType("music").Process(albumPath)
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
                // A fresh context per parallel iteration, deliberately — DbContext is
                // not thread-safe, and the manager stack below used to be built once
                // outside this loop and shared across every concurrent iteration.
                // EF Core does not queue concurrent operations on one context; it
                // throws "a second operation was started on this context instance
                // before a previous operation completed", which is what 282 of the
                // stored FailedJobs rows actually are.
                await using MediaContext context = new();
                MusicGenreRepository musicGenreRepository = new(context);
                ArtistRepository artistRepository = new(context);
                RecordingRepository recordingRepository = new(context);
                RecordingManager recordingManager = new(
                    recordingRepository,
                    musicGenreRepository,
                    artistRepository,
                    StorageDriver,
                    StorageFactory,
                    LoggerFactory.CreateLogger<RecordingManager>()
                );

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
                        // Same reason as above: its own context, not the outer
                        // iteration's — two nested parallel loops sharing one
                        // context multiplies the race rather than avoiding it.
                        await using MediaContext artistContext = new();
                        MusicGenreRepository artistGenreRepository = new(artistContext);
                        ArtistRepository perArtistRepository = new(artistContext);
                        ArtistManager artistManager = new(
                            perArtistRepository,
                            artistGenreRepository,
                            jobDispatcher,
                            StorageFactory,
                            LoggerFactory.CreateLogger<ArtistManager>()
                        );

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
