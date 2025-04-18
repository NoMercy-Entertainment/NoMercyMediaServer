using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Encoder;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class EncodeMusicJob : AbstractMusicEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 7;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using QueueContext queueContext = new();
        await using LibraryRepository libraryRepository = new(context);
        
        JobDispatcher jobDispatcher = new();
        
        MusicGenreRepository musicGenreRepository = new(context);
        
        ArtistRepository artistRepository = new(context);
        ArtistManager artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);
        
        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(recordingRepository, musicGenreRepository);
        
        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository);
        
        Library albumLibrary = await context.Libraries
            .Where(f => f.Id == Folder.Id)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();
        
        try
        {
            BaseContainer container = BaseContainer.Create(Profile.Container);

            Track track = new()
            {
                Id = foundTrack.Id,
                Name = foundTrack.Title,
                FolderId = Folder.Id,
                TrackNumber = foundTrack.Position
            };

            BuildAudioStreams(Profile, ref container, foundTrack, folderMetaData.MusicBrainzRelease);

            VideoAudioFile ffmpeg = new FfMpeg()
                .Open(mediaFile.Path);

            ffmpeg.SetBasePath(folderMetaData.BasePath);
            ffmpeg.SetTitle(mediaFile.Name);
            ffmpeg.ToFile(track.CreateFileName());

            ffmpeg.AddContainer(container);

            ffmpeg.Prioritize();

            ffmpeg.Build();

            string fullCommand = ffmpeg.GetFullCommand();

            ProgressMeta progressMeta = new()
            {
                Id = Id,
                Title = foundTrack.Title,
                BaseFolder = folderMetaData.BasePath
            };
            
            Logger.Encoder(fullCommand);
            await ffmpeg.Run(fullCommand, folderMetaData.BasePath, progressMeta);
            
            
            fileManager.FilterFiles(container.FileName);
                
            await fileManager.FindFiles(Id, albumLibrary);
            
            // await using MediaScan mediaScan = new();
            // MediaFolderExtend mediaFolder =
            //     (await mediaScan.DisableRegexFilter().EnableFileListing().Process(folderMetaData.BasePath)).First();
            //
            // foreach(MusicBrainzMedia media in folderMetaData.MusicBrainzRelease.Media)
            // {
            //     if (!await recordingManager.Store(folderMetaData.MusicBrainzRelease, foundTrack, media,
            //         Folder, mediaFolder, null)) continue;
            //
            //     foreach (ReleaseArtistCredit artist in foundTrack.ArtistCredit)
            //     {
            //         await artistManager.Store(artist.MusicBrainzArtist, albumLibrary, Folder, mediaFolder, foundTrack);
            //
            //         jobDispatcher.DispatchJob<MusicDescriptionJob>(artist.MusicBrainzArtist);
            //     }
            // }

            Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
            {
                Id = Id,
                Status = "completed",
                Title = foundTrack.Title,
                Message = "Done",
            });
        }
        catch (Exception e)
        {
            Logger.Encoder(e, LogEventLevel.Error);

            Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
            {
                Id = Id,
                Status = "failed",
                Title = foundTrack.Title,
                Message = e.Message,
            });
        }
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