using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Encoder;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class EncodeMusicJob : AbstractMusicEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 4;

    public string Status { get; set; } = "pending";

    public string InputFolder { get; set; } = string.Empty;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using QueueContext queueContext = new();

        await using LibraryRepository libraryRepository = new(context);
        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
        {
            Logger.Encoder($"Folder not found: {FolderId}", LogEventLevel.Verbose);
            return;
        }

        List<EncoderProfile> profiles = folder.EncoderProfileFolder
            .Select(e => e.EncoderProfile)
            .ToList();

        if (profiles.Count == 0)
        {
            Logger.Encoder($"No encoder profiles found for folder: {folder.Id}", LogEventLevel.Verbose);
            return;
        }

        FolderMetadata? folderMetaData = await GetFolderMetaData(folder);
        if (folderMetaData is null)
        {
            Logger.Encoder($"Folder metadata not found for folder: {folder.Id}", LogEventLevel.Verbose);
            return;
        }

        if (!Directory.Exists(folderMetaData.BasePath))
        {
            Directory.CreateDirectory(folderMetaData.BasePath);
            Logger.Encoder($"{folderMetaData.BasePath} is created", LogEventLevel.Verbose);
        }
        
        foreach (EncoderProfile profile in profiles)
        {
            foreach (MediaFile mediaFile in folderMetaData.Files)
            {
                if (string.IsNullOrEmpty(mediaFile.Path))
                {
                    Logger.Encoder($"File path is empty: {mediaFile.Name}", LogEventLevel.Verbose);
                    continue;
                }

                MusicBrainzTrack? foundTrack = folderMetaData.MusicBrainzRelease.Media.SelectMany(x => x.Tracks)
                    .FirstOrDefault(x => x.Title.ContainsSanitized(mediaFile.Name));
                
                if (foundTrack is null)
                {
                    Logger.Encoder($"Track not found in MusicBrainz: {mediaFile.Name}", LogEventLevel.Verbose);
                    continue;
                }

                try
                {
                    BaseContainer container = BaseContainer.Create(profile.Container);
                
                    Track track = new()
                    {
                        Id = folderMetaData.MusicBrainzRelease.Id,
                        Name = foundTrack.Title,
                        FolderId = folder.Id,
                        TrackNumber = foundTrack.Position
                    };

                    BuildAudioStreams(profile, ref container);

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
                    
                    // Logger.Encoder(ffmpeg);
                    Logger.Encoder(fullCommand);
                    // await ffmpeg.Run(fullCommand, mediaFile.Path, progressMeta);

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

                    // Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
                    // {
                    //     Id = Id,
                    //     Status = "failed",
                    //     Title = fileMetadata.Title,
                    //     Message = e.Message,
                    // });

                    throw;
                }
            }
        }
    }

    private async Task<FolderMetadata?> GetFolderMetaData(Folder folder)
    {
        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        MusicBrainzReleaseAppends? musicBrainzRelease = await musicBrainzReleaseClient.WithAllAppends(Id);
        if (
            musicBrainzRelease is null ||
            string.IsNullOrEmpty(musicBrainzRelease.Title) ||
            string.IsNullOrEmpty(musicBrainzRelease.ArtistCredit?.FirstOrDefault()?.Name)
        )
        {
            return null;
        }

        string artistName = musicBrainzRelease.ArtistCredit.FirstOrDefault()?.Name ?? string.Empty;
        string releaseName = musicBrainzRelease.Title;
        string year = musicBrainzRelease.DateTime?.Year.ToString() ?? string.Empty;
        string folderReleaseName = $"[{year}] {releaseName}";
        string folderStartLetter = artistName[0..1];

        if (Regex.Match(folderStartLetter, "/[^a-zA-Z0-9]/").Success)
        {
            folderStartLetter = "[Other]";
        }
        else if (Regex.Match(folderStartLetter, "/[0-9]/").Success)
        {
            folderStartLetter = "#";
        }
        else
        {
            folderStartLetter = folderStartLetter.ToUpper();
        }

        string basePath = Path.Combine(folder.Path, folderStartLetter, artistName,
            folderReleaseName.DirectorySafeName());

        await using MediaScan mediaScan = new();

        ConcurrentBag<MediaFolderExtend> mediaFiles =
            await mediaScan.DisableRegexFilter().EnableFileListing().Process(InputFolder);
        ConcurrentBag<MediaFile> files = mediaFiles.First()?.Files ?? [];

        return new FolderMetadata
        {
            MusicBrainzRelease = musicBrainzRelease,
            BasePath = basePath,
            Files = files,
            ArtistName = artistName,
            ReleaseName = releaseName,
            Year = int.Parse(year, CultureInfo.InvariantCulture),
            FolderReleaseName = folderReleaseName,
            FolderStartLetter = folderStartLetter
        };
    }

    private record FolderMetadata
    {
        public MusicBrainzReleaseAppends MusicBrainzRelease { get; set; } = null!;
        public string BasePath { get; set; } = string.Empty;
        public ConcurrentBag<MediaFile> Files { get; set; } = [];
        public string ArtistName { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FolderReleaseName { get; set; } = string.Empty;
        public string FolderStartLetter { get; set; } = string.Empty;
    }

    private static void BuildAudioStreams(EncoderProfile encoderProfile, ref BaseContainer container)
    {
        foreach (IAudioProfile profile in encoderProfile.AudioProfiles)
        {
            BaseAudio stream = BaseAudio.Create(profile.Codec)
                .SetAudioChannels(profile.Channels)
                .SetAllowedLanguages(profile.AllowedLanguages)
                .SetHlsSegmentFilename("")
                .SetHlsPlaylistFilename("")
                .AddOpts(profile.Opts)
                .AddCustomArguments(profile.CustomArguments);

            container.AddStream(stream);
        }
    }
}