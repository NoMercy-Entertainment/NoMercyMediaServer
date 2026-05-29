using System.Diagnostics;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.Encoder.Composition;
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
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class MusicEncodeJob : AbstractMusicEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 3;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        // TODO: cross-backend transfer — MediaFile.Path (source) and FolderMetaData.BasePath
        // (encode output) both resolve through the same folder's StorageDriver today. When
        // source and destination cross folder boundaries, staged copy semantics will be needed.
        await using MediaContext context = new();

        await using LibraryRepository libraryRepository = new(context, StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        List<EncoderProfile> profiles = folder
            .EncoderProfileFolder.Select(e => e.EncoderProfile)
            .ToList();

        foreach (EncoderProfile profile in profiles)
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
                if (profile.AudioProfiles.Length == 0)
                {
                    Logger.Encoder(
                        $"Skipping profile {profile.Name}: no audio profiles configured"
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
                            ProfileName = profile.Name,
                        }
                    );
                }

                EncodingProfile encodingProfile = V2ProfileFactory.FromV1(
                    profile.Id,
                    profile.Name,
                    profile.Container ?? "mp3",
                    [],
                    profile
                        .AudioProfiles.Select(a => new V1AudioProfile(
                            a.Codec,
                            a.Channels,
                            a.SampleRate,
                            a.SegmentName,
                            a.PlaylistName,
                            a.AllowedLanguages,
                            a.CustomArguments,
                            a.Loudness,
                            a.Downmix,
                            a.CustomPanMatrix
                        ))
                        .ToArray(),
                    profile
                        .SubtitleProfiles.Select(s => new V1SubtitleProfile(
                            s.Codec,
                            s.PlaylistName,
                            s.AllowedLanguages,
                            s.CustomArguments
                        ))
                        .ToArray()
                );

                IEncodingOrchestrator orchestrator =
                    EncoderProvider.ResolveService<IEncodingOrchestrator>()
                    ?? throw new InvalidOperationException(
                        "IEncodingOrchestrator is not registered. Did AddNoMercyEncoder() run?"
                    );

                // Resolve per-folder storage so the encoder operates under the
                // correct backend and path guard for this library folder.
                // TODO: cross-backend transfer — when source and output folders
                // map to different backends, staged copy semantics will be needed.
                // For now source == destination (same folder).
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

                Logger.Encoder(
                    $"Encoded {MediaFile.Path} → {encodeResult.OutputPath} in {encodeResult.Duration.TotalSeconds:F1}s ({encodeResult.Metrics?.EncoderUsed ?? "unknown"})"
                );

                await AddRecording(folder);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = track.Id,
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
                Logger.Encoder(e, LogEventLevel.Error);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = track.Id,
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
        ArtistManager artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);

        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(
            recordingRepository,
            musicGenreRepository,
            artistRepository,
            StorageDriver
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
            Config.ParallelOptions,
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
                    Logger.MusicBrainz(
                        $"Album Library not found: {LibraryId}",
                        LogEventLevel.Error
                    );
                    return;
                }

                await Parallel.ForEachAsync(
                    FoundTrack.ArtistCredit,
                    Config.ParallelOptions,
                    async (artist, _) =>
                    {
                        Logger.MusicBrainz(
                            $"Storing Artist: {artist.MusicBrainzArtist.Name}",
                            LogEventLevel.Verbose
                        );
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
