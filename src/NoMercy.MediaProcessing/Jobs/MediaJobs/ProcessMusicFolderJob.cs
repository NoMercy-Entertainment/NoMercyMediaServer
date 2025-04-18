using System.Collections.Concurrent;
using System.Globalization;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class ProcessMusicFolderJob : AbstractMusicFolderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 4;

    public string Status { get; set; } = "pending";

    public string InputFolder { get; set; } = string.Empty;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using QueueContext queueContext = new();
        JobDispatcher jobDispatcher = new();

        await using LibraryRepository libraryRepository = new(context);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
        {
            Logger.Encoder($"Folder not found: {FolderId}", LogEventLevel.Error);
            return;
        }

        List<EncoderProfile> profiles = folder.EncoderProfileFolder
            .Select(e => e.EncoderProfile)
            .ToList();

        if (profiles.Count == 0)
        {
            Logger.Encoder($"No encoder profiles found for folder: {folder.Id}", LogEventLevel.Error);
            return;
        }

        FolderMetadata? folderMetaData = await GetFolderMetaData(folder);
        if (folderMetaData is null)
        {
            Logger.Encoder($"Folder metadata not found for folder: {folder.Id}", LogEventLevel.Error);
            return;
        }

        if (!Directory.Exists(folderMetaData.BasePath))
        {
            Directory.CreateDirectory(folderMetaData.BasePath);
            Logger.Encoder($"{folderMetaData.BasePath} is created", LogEventLevel.Verbose);
        }
        
        await using MediaScan mediaScan = new();
        MediaFolderExtend mediaFolder =
            (await mediaScan.DisableRegexFilter().EnableFileListing().Process(folderMetaData.BasePath)).First();
        
        Logger.App("Matched: " + folderMetaData.ReleaseName + " - " + Id);
        AddReleaseJob addReleaseOnlyJob = new AddReleaseJob()
        {
            LibraryId = folder.FolderLibraries.First().LibraryId,
            Id = Id,
            BaseFolder = folder,
            MediaFolder = mediaFolder
        };
        await addReleaseOnlyJob.Handle();
        
        foreach (EncoderProfile profile in profiles)
        {
            int fileCount = 0;
            IOrderedEnumerable<MediaFile> files = folderMetaData.Files.OrderBy(x => x.Name);
            foreach (MediaFile mediaFile in files)
            {
                fileCount++;
                if (string.IsNullOrEmpty(mediaFile.Path))
                {
                    Logger.Encoder($"File path is empty: {mediaFile.Name}", LogEventLevel.Error);
                    continue;
                }
                TagLib.File tagFile = TagLib.File.Create(mediaFile.Path);
                string recordingName = tagFile.Tag.Title ?? mediaFile.Name;
                int trackNumber = tagFile.Tag.Track > 0 ? (int)tagFile.Tag.Track : fileCount;
                MusicBrainzTrack? foundTrack = folderMetaData.MusicBrainzRelease.Media.SelectMany(x => x.Tracks)
                    .FirstOrDefault(x =>
                        (
                            x.Title.ContainsSanitized(recordingName) &&
                            x.Position == trackNumber
                        ) ||
                        (
                            x.Title.ContainsSanitized(mediaFile.Name) &&
                            x.Position == trackNumber
                        )
                    );
                if (foundTrack is null)
                {
                    foundTrack = folderMetaData.MusicBrainzRelease.Media.SelectMany(x => x.Tracks)
                        .FirstOrDefault(x =>x.Position == trackNumber);
                    if (foundTrack is null)
                    {
                        Logger.Encoder($"Track not found in MusicBrainz: {recordingName}", LogEventLevel.Error);
                        continue;
                    }
                }
                
                jobDispatcher.DispatchJob<EncodeMusicJob>(
                    profile, 
                    folder, 
                    folderMetaData, 
                    mediaFile,
                    foundTrack 
                );
                
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
            string.IsNullOrEmpty(musicBrainzRelease.ArtistCredit.FirstOrDefault()?.Name)
        )
        {
            return null;
        }

        string artistName = musicBrainzRelease.ArtistCredit.FirstOrDefault()?.Name ?? string.Empty;
        string releaseName = musicBrainzRelease.Title;
        string year = musicBrainzRelease.DateTime?.Year.ToString() ?? string.Empty;
        string folderReleaseName = $"[{year}] {releaseName}";
        string folderStartLetter = artistName[..1];

        if (folderStartLetter.IsAlphaNumeric())
        {
            folderStartLetter = "[Other]";
        }
        else if (folderStartLetter.IsNumeric())
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

        MediaFolderExtend mediaFolder =
            (await mediaScan.DisableRegexFilter().EnableFileListing().Process(InputFolder)).First();
        ConcurrentBag<MediaFile> files = mediaFolder.Files ?? [];

        return new()
        {
            MusicBrainzRelease = musicBrainzRelease,
            BasePath = basePath,
            Files = files,
            ArtistName = artistName,
            ReleaseName = releaseName,
            Year = int.Parse(year, CultureInfo.InvariantCulture),
            FolderReleaseName = folderReleaseName,
            FolderStartLetter = folderStartLetter,
            
        };
    }

    public record FolderMetadata
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
}