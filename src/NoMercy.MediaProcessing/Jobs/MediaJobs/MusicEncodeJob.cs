using System.Diagnostics;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class MusicEncodeJob : AbstractMusicEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 3;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        await using MediaContext context = new();

        await using LibraryRepository libraryRepository = new(context);

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

                // TODO(encoder-v3): Deserialize V3 EncodingProfile from profile.Param
                // TODO(encoder-v3): Resolve IEncoder from DI
                // TODO(encoder-v3): Call encoder.EncodeAsync() with EventBusProgressObserver
                // For now, log that V3 encoding would happen here
                Logger.Encoder(
                    $"V3 encoder: would encode {MediaFile.Path} → {FolderMetaData.BasePath} with profile {profile.Name}"
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
            artistRepository
        );

        await using MediaScan mediaScan = new();

        // TODO(encoder-v3): FilterByFileName needs actual encoded output filename once V3 is wired
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
