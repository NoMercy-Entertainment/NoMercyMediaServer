using System.Diagnostics;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.Encoder;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
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
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class EncodeMusicJob : AbstractMusicEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 3;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using QueueContext queueContext = new();

        await using LibraryRepository libraryRepository = new(context);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null) return;

        List<EncoderProfile> profiles = folder.EncoderProfileFolder
            .Select(e => e.EncoderProfile)
            .ToList();

        foreach (EncoderProfile profile in profiles)
        {
            Track track = new()
            {
                Id = FoundTrack.Id,
                Name = FoundTrack.Title,
                FolderId = folder.Id,
                TrackNumber = FoundTrack.Position
            };

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(new EncodingStartedEvent
                    {
                        JobId = track.Id.GetHashCode(),
                        InputPath = MediaFile.Path,
                        OutputPath = FolderMetaData.BasePath,
                        ProfileName = profile.Name
                    });
                }

                BaseContainer container = BaseContainer.Create(profile.Container);

                BuildAudioStreams(profile, ref container, FoundTrack, FolderMetaData.MusicBrainzRelease);

                VideoAudioFile ffmpeg = new FfMpeg()
                    .Open(MediaFile.Path);

                ffmpeg.SetBasePath(FolderMetaData.BasePath);
                ffmpeg.SetTitle(MediaFile.Name);
                ffmpeg.ToFile(track.CreateTitle());

                ffmpeg.AddContainer(container);

                ffmpeg.Prioritize();

                ffmpeg.Build();

                string fullCommand = ffmpeg.GetFullCommand();

                ProgressMeta progressMeta = new()
                {
                    Id = track.Id,
                    Title = FoundTrack.Title,
                    BaseFolder = FolderMetaData.BasePath,
                    Type = "audio"
                };

                Logger.Encoder(fullCommand);
                await ffmpeg.Run(fullCommand, FolderMetaData.BasePath, progressMeta);

                await AddRecording(container, folder);

                Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
                {
                    Id = track.Id,
                    Status = "completed",
                    Title = FoundTrack.Title,
                    Message = "Done"
                });

                if (EventBusProvider.IsConfigured)
                {
                    stopwatch.Stop();
                    await EventBusProvider.Current.PublishAsync(new EncodingCompletedEvent
                    {
                        JobId = track.Id.GetHashCode(),
                        OutputPath = FolderMetaData.BasePath,
                        Duration = stopwatch.Elapsed
                    });
                }
            }
            catch (Exception e)
            {
                Logger.Encoder(e, LogEventLevel.Error);

                Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
                {
                    Id = track.Id,
                    Status = "failed",
                    Title = FoundTrack.Title,
                    Message = e.Message
                });

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(new EncodingFailedEvent
                    {
                        JobId = track.Id.GetHashCode(),
                        InputPath = MediaFile.Path,
                        ErrorMessage = e.Message,
                        ExceptionType = e.GetType().Name
                    });
                }
            }
        }
    }

    private async Task AddRecording(BaseContainer container, Folder folder)
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MusicGenreRepository musicGenreRepository = new(context);

        ArtistRepository artistRepository = new(context);
        ArtistManager artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);

        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(recordingRepository, musicGenreRepository, artistRepository);

        await using MediaScan mediaScan = new();

        MediaFolderExtend mediaFolder = (
            await mediaScan
                .EnableFileListing()
                .FilterByMediaType("music")
                .FilterByFileName(container.FileName)
                .Process(FolderMetaData.BasePath)
        ).First();

        mediaFolder.Files?.FilterConcurrentBag([container.FileName]);

        CoverArtImageManagerManager.CoverPalette? coverPalette =
            await CoverArtImageManagerManager.Add(FolderMetaData.MusicBrainzRelease.MusicBrainzReleaseGroup.Id);

        await Parallel.ForEachAsync(FolderMetaData.MusicBrainzRelease.Media, Config.ParallelOptions, async (media, t) =>
        {
            if (!await recordingManager.Store(FolderMetaData.MusicBrainzRelease, FoundTrack, media,
                    folder, mediaFolder, coverPalette)) return;

            Library? albumLibrary = folder.FolderLibraries
                .FirstOrDefault(f => f.LibraryId == LibraryId)?.Library;

            if (albumLibrary is null)
            {
                Logger.MusicBrainz($"Album Library not found: {LibraryId}", LogEventLevel.Error);
                return;
            }

            await Parallel.ForEachAsync(FoundTrack.ArtistCredit, Config.ParallelOptions, async (artist, _) =>
            {
                Logger.MusicBrainz($"Storing Artist: {artist.MusicBrainzArtist.Name}", LogEventLevel.Verbose);
                await artistManager.Store(artist.MusicBrainzArtist, albumLibrary, folder, mediaFolder, FoundTrack);

                jobDispatcher.DispatchJob<MusicDescriptionJob>(artist.MusicBrainzArtist);
            });
        });
    }

    private static void BuildAudioStreams(EncoderProfile encoderProfile, ref BaseContainer container,
        MusicBrainzTrack track, MusicBrainzReleaseAppends musicBrainzRelease)
    {
        foreach (IAudioProfile profile in encoderProfile.AudioProfiles)
        {
            MusicBrainzMedia album = musicBrainzRelease.Media
                .First(m => m.Tracks.Any(t => t.Title == track.Title));

            string albumArtist = string.Join("/", musicBrainzRelease.ArtistCredit.Select(c => c.Name));
            string albumNumber = musicBrainzRelease.Title;
            string artist = string.Join("/", track.ArtistCredit.Select(c => c.Name));
            int date = musicBrainzRelease.ReleaseEvents?[0].DateTime.ParseYear() ??
                       track.Recording.FirstReleaseDate.ParseYear();
            string disambiguation = track.Recording.Disambiguation;
            string discNumber = $"{album.Position}/{musicBrainzRelease.Media.Length}";
            string genre = string.Join("/",
                musicBrainzRelease.MusicBrainzReleaseGroup?.Genres?.Select(c => c.Name) ?? []);
            string title = track.Title;
            string trackNumber = $"{track.Number}/{album.TrackCount}";

            Guid musicBrainzReleaseId = musicBrainzRelease.Id;
            Guid musicBrainzRecordingId = track.Id;
            Guid musicBrainzTrackId = track.Id;
            Guid musicBrainzArtistId = track.ArtistCredit[0].MusicBrainzArtist.Id;
            Guid musicBrainzAlbumArtistId = musicBrainzRelease.ArtistCredit[0].MusicBrainzArtist.Id;
            Guid? musicBrainzReleaseGroupId = musicBrainzRelease.MusicBrainzReleaseGroup?.Id;

            BaseAudio stream = BaseAudio.Create(profile.Codec)
                .SetAudioChannels(profile.Channels)
                .SetAllowedLanguages(profile.AllowedLanguages)
                .SetSampleRate(profile.SampleRate)
                .SetHlsSegmentFilename(profile.SegmentName)
                .SetHlsPlaylistFilename(profile.PlaylistName)
                .AddOpts(profile.Opts)
                .AddCustomArguments(profile.CustomArguments)
                .SetLanguage(musicBrainzRelease.MusicBrainzTextRepresentation.Language)
                .AddId3Tags(new()
                {
                    { "album", albumNumber },
                    { "album_artist", albumArtist },
                    { "artist", artist },
                    { "date", date.ToString() },
                    { "disc", discNumber },
                    { "genre", genre },
                    { "title", title },
                    { "track", trackNumber },
                    { "Disambiguation", disambiguation },
                    { "MusicBrainzReleaseId", musicBrainzReleaseId },
                    { "MusicBrainzRecordingId", musicBrainzRecordingId },
                    { "MusicBrainzTrackId", musicBrainzTrackId },
                    { "MusicBrainzArtistId", musicBrainzArtistId },
                    { "MusicBrainzAlbumArtistId", musicBrainzAlbumArtistId },
                    { "MusicBrainzReleaseGroupId", musicBrainzReleaseGroupId }
                });

            container.AddStream(stream);
        }
    }
}