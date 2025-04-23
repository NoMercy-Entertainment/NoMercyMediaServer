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
using TagLib;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class ProcessMusicFolderJob : AbstractMusicFolderJob
{
    public override string QueueName => "queue";
    public override int Priority => 7;

    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        await using MediaContext context = new();
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
            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode unixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                
                Directory.CreateDirectory(folderMetaData.BasePath, unixFileMode);
            }
            else
            {
                Directory.CreateDirectory(folderMetaData.BasePath);
            }
            Logger.Encoder($"{folderMetaData.BasePath} is created", LogEventLevel.Verbose);
        }
        
        await using MediaScan mediaScan = new();
        
        MediaFolderExtend? mediaFolder = (
            await mediaScan
                .EnableFileListing()
                .FilterByMediaType("music")
                .Process(folderMetaData.BasePath, 1)
        ).FirstOrDefault();
        
        if (mediaFolder is null)
        {
            Logger.Encoder($"Media folder not found for: {folderMetaData.BasePath}", LogEventLevel.Error);
            return;
        }
        
        mediaFolder.Files?.Clear();
        
        Logger.App("Matched: " + folderMetaData.ReleaseName + " - " + Id);
        AddReleaseOnlyJob addReleaseOnlyJob = new()
        {
            LibraryId = LibraryId,
            Id = Id,
            BaseFolder = folder,
            MediaFolder = mediaFolder,
        };
        await addReleaseOnlyJob.Handle();
        
        string[] extensions = [".mp3", ".flac", ".wav", ".m4a"];
        List<MediaFile> files = folderMetaData.Files
            .Where(f => f.Type == "file" && extensions.Contains(f.Extension))
            .OrderBy(x => x.Parsed?.DiscNumber)
            .ThenBy(x => x.Parsed?.TrackNumber)
            .ToList();

        foreach (EncoderProfile profile in profiles)
        {
            foreach (MediaFile? mediaFile in files)
            {
                TagLib.File tagFile = TagLib.File.Create(mediaFile.Path);
                Tag? tag = tagFile.Tag;
                string recordingName = tag.Title ?? mediaFile.Name;
                int albumNumber = tag.Disc.ToInt();
                int trackNumber = tag.Track > 0 ? tag.Track.ToInt() : files.IndexOf(mediaFile) + 1;
                MusicBrainzTrack? foundTrack = folderMetaData.MusicBrainzRelease.Media.SelectMany(x => x.Tracks)
                    .FirstOrDefault(x =>
                        (
                            x.Title.ContainsSanitized(recordingName) &&
                            x.Position == trackNumber
                        ) ||
                        (
                            x.Title.ContainsSanitized(mediaFile.Name) &&
                            x.Title.ContainsSanitized(recordingName) &&
                            x.Position == trackNumber
                        ));
                
                if (foundTrack is null)
                {
                    foundTrack = folderMetaData.MusicBrainzRelease.Media
                        .Where(x => x.Position == albumNumber)
                        .SelectMany(x => x.Tracks)
                        .FirstOrDefault(x => x.Position == trackNumber);
                    if (foundTrack is null)
                    {
                        foundTrack = folderMetaData.MusicBrainzRelease.Media
                            .SelectMany(x => x.Tracks)
                            .FirstOrDefault(x => x.Position == trackNumber);
                        
                        if (foundTrack is null)
                        {
                            Logger.Encoder($"Track not found in MusicBrainz: {recordingName}", LogEventLevel.Error);
                            continue;
                        }
                    }
                }
                
                folderMetaData.Files.Clear();
                
                jobDispatcher.DispatchJob<EncodeMusicJob>(
                    foundTrack.Id,
                    profile,
                    folder,
                    folderMetaData,
                    mediaFile,
                    foundTrack,
                    LibraryId,
                    FilePath,
                    mediaFile.Path
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

        MediaFolderExtend mediaFolder = (
            await mediaScan
                .EnableFileListing()
                .FilterByMediaType("music")
                .Process(FilePath)
        ).First();
        ConcurrentBag<MediaFile> files = mediaFolder.Files ?? [];

        return new()
        {
            MusicBrainzRelease = musicBrainzRelease,
            BasePath = basePath,
            Files = files.ToList(),
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
        public List<MediaFile> Files { get; set; } = [];
        public string ArtistName { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FolderReleaseName { get; set; } = string.Empty;
        public string FolderStartLetter { get; set; } = string.Empty;
    }
}