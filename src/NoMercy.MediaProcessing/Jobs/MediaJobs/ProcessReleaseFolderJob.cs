// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public partial class ProcessReleaseFolderJob : AbstractMusicFolderJob
{
    public override string QueueName => "queue";
    public override int Priority => 4;

    private bool _fromFingerprint = false;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        Library albumLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();

        Logger.App("Processing music folder " + FilePath);

        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend>? rootFolders = await mediaScan
            .DisableRegexFilter()
            .EnableFileListing()
            .Process(FilePath, 2);

        if (rootFolders.Count == 0)
        {
            Logger.App("No folders found in " + FilePath);
            return;
        }

        await ScanForReleases(rootFolders, albumLibrary, jobDispatcher);
    }

    private async Task ScanForReleases(
        ConcurrentBag<MediaFolderExtend>? rootFolders,
        Library albumLibrary,
        JobDispatcher jobDispatcher
    )
    {
        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        using MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        foreach (MediaFolderExtend folder in rootFolders ?? [])
        {
            if (folder?.Files is null || folder.Files.IsEmpty || folder.Files.Count == 0)
            {
                continue;
            }

            Init(folder,
                out string artistName,
                out string albumName,
                out int year,
                out string releaseType,
                out string libraryFolder);

            if (!BaseFolderCheck(albumLibrary, libraryFolder, folder, out Folder? baseFolder)) continue;
            if (baseFolder is null) continue;

            Logger.App("Processing: " + baseFolder.Path + " - " + libraryFolder + " - " + artistName + " - " + albumName + " - " + year + " - " + releaseType);

            MusicBrainzRelease? release = await MusicBrainzRelease(musicBrainzReleaseClient, musicBrainzRecordingClient, folder, artistName, albumName, artistName, year);

            _fromFingerprint = false;

            if (release is null) continue;

            Logger.App("Matched: " + release.Title + " - " + release.Id);
            jobDispatcher.DispatchJob<AddReleaseJob>(LibraryId, release.Id, baseFolder, folder);
        }
    }

    private bool BaseFolderCheck(Library albumLibrary, string libraryFolder, MediaFolderExtend folder,
        out Folder? baseFolder)
    {
        Folder? foundBaseFolder = albumLibrary.FolderLibraries
            .Select(folderLibrary => folderLibrary.Folder)
            .FirstOrDefault(f => f.Path == libraryFolder || f.Path == folder.Path);

        if (foundBaseFolder is null)
        {
            Logger.App("No base folder found for: " + folder.Path);
            baseFolder = null;
            return false;
        }
        baseFolder = foundBaseFolder;
        return true;
    }

    private async Task<MusicBrainzRelease?> MusicBrainzRelease(
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        MusicBrainzRecordingClient musicBrainzRecordingClient,
        MediaFolderExtend folder,
        string artist,
        string albumName,
        string artistName,
        int year)
    {
        MusicBrainzRelease[] musicBrainzReleases = await SearchReleaseByQuery(
            musicBrainzReleaseClient,
            albumName,
            artistName,
            year);

        MusicBrainzRelease? bestMatchedRelease = await GetBestMatchedRelease(
            musicBrainzReleaseClient,
            folder,
            musicBrainzReleases);

        MusicBrainzRelease? realRelease = await CheckBestRelease(
            musicBrainzReleaseClient,
            musicBrainzRecordingClient,
            folder,
            bestMatchedRelease,
            artist,
            albumName,
            year);

        return realRelease;
    }

    private async Task<MusicBrainzRelease?> CheckBestRelease(
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        MusicBrainzRecordingClient musicBrainzRecordingClient,
        MediaFolderExtend folder,
        MusicBrainzRelease? bestMatchedRelease,
        string artist,
        string albumName,
        int year,
        bool fingerPrint = false)
    {
        if (bestMatchedRelease is not null) return bestMatchedRelease;

        switch (fingerPrint)
        {
            case false:
            {
                MusicBrainzRelease[] releases = await SearchRecordingByQuery(musicBrainzRecordingClient, folder, artist, albumName, year);
                bestMatchedRelease = await GetBestMatchedRelease(
                    musicBrainzReleaseClient,
                    folder,
                    releases);

                return await CheckBestRelease(
                    musicBrainzReleaseClient,
                    musicBrainzRecordingClient,
                    folder,
                    bestMatchedRelease,
                    artist,
                    albumName,
                    year,
                    true);
            }
            case true when !_fromFingerprint:
            {
                MusicBrainzRelease[] releases = await SearchRecordingByFingerprint(folder, albumName);
                bestMatchedRelease = await GetBestMatchedRelease(
                    musicBrainzReleaseClient,
                    folder,
                    releases);

                return await CheckBestRelease(
                    musicBrainzReleaseClient,
                    musicBrainzRecordingClient,
                    folder,
                    bestMatchedRelease,
                    artist,
                    albumName,
                    year,
                    true);
            }
            default:
                Logger.App("No match found for: " + folder.Path);
                return null;
        }
    }

    private static void Init(MediaFolderExtend folder,
        out string artistName,
        out string albumName,
        out int year,
        out string releaseType,
        out string libraryFolder)
    {
        char separator = Path.DirectorySeparatorChar;
        string pattern = $@"\{separator}#\{separator}|\{separator}\[Other\]\{separator}|\{separator}\[Soundtrack\]\{separator}|\{separator}\[Various Artists\]\{separator}|\{separator}[A-Z]\{separator}";
        string rawFolderName = Regex.Replace(folder.Name, @"\[\d{4}\]\s?", "");
        Match match = PathRegex().Match(folder.Path);
        artistName = match.Groups["artist"].Success ? match.Groups["artist"].Value : string.Empty;
        albumName = match.Groups["album"].Success ? match.Groups["album"].Value : rawFolderName;
        year = match.Groups["year"].Success ? Convert.ToInt32(match.Groups["year"].Value) : 0;
        releaseType = match.Groups["releaseType"].Success ? match.Groups["releaseType"].Value : string.Empty;
        libraryFolder = (match.Groups["library_folder"].Success ? match.Groups["library_folder"].Value : null) ??
                        Regex.Split(folder.Path, pattern)?[0] ?? string.Empty;
    }

    private async Task<MusicBrainzRelease?> GetBestMatchedRelease(
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        MediaFolderExtend folder,
        MusicBrainzRelease[] matchedReleases)
    {
        int highestScore = 0;

        MusicBrainzRelease? bestRelease = null;
        switch (matchedReleases.Length)
        {
            case 0:
                return null;
            case 1:
                return matchedReleases[0];
        }

        foreach (MusicBrainzRelease? release in matchedReleases)
        {
            ConcurrentBag<MediaFile> files = folder?.Files ?? [];
            MusicBrainzReleaseAppends? result = await musicBrainzReleaseClient.WithAllAppends(release.Id);

            int score = CalculateMatchScore(result, files);
            if (score <= highestScore) continue;

            highestScore = score;
            bestRelease = release;
        }

        return bestRelease;
    }

    private int CalculateMatchScore(MusicBrainzRelease? release, ConcurrentBag<MediaFile> localTracks)
    {
        MusicBrainzTrack[] tracks = release?.Media?.SelectMany(m => m?.Tracks ?? [])?.DistinctBy(r => r.Id)?.ToArray() ?? [];
        if (tracks.Length == 0) return 0;

        int score = 0;
        foreach (MusicBrainzTrack track in tracks)
        {
            if (localTracks.FirstOrDefault(t =>
                (CompareTrackName(t, track) && CompareTrackDuration(t, track))
                || (CompareTrackName(t, track) && CompareTrackNumber(t, track))
            ) == null)
            {
                continue;
            }
            score++;
        }
        return score;
    }

    private bool CompareTrackDuration(MediaFile mediaFile, MusicBrainzTrack track)
    {
        double duration = track.Duration;
        double fileDuration = mediaFile.FFprobe!.Duration.TotalSeconds;
        if (duration == 0 || fileDuration == 0) return true;
        return Math.Abs(duration - fileDuration) < 10;
    }

    private bool CompareTrackNumber(MediaFile mediaFile, MusicBrainzTrack track)
    {
        int trackNumber = track.Position;
        int fileTrackNumber = mediaFile.Parsed?.TrackNumber ?? 0;

        if (trackNumber == 0 || fileTrackNumber == 0) return true;
        return Math.Abs(trackNumber - fileTrackNumber) == 0;
    }

    private static bool CompareTrackName(MediaFile mediaFile, MusicBrainzTrack track)
    {
        string title = mediaFile.Parsed?.Title ?? Path.GetFileNameWithoutExtension(mediaFile.Name);
        string trackTitle = track.Title;
        if (string.IsNullOrEmpty(trackTitle) || string.IsNullOrEmpty(title)) return true;
        return title.ContainsSanitized(trackTitle);
    }

    private async Task<MusicBrainzRelease[]> SearchRecordingByQuery(
        MusicBrainzRecordingClient musicBrainzRecordingClient,
        MediaFolderExtend folder,
        string artist,
        string albumName,
        int year)
    {
        List<MusicBrainzRelease> musicBrainzReleases = [];

        foreach (MediaFile file in folder?.Files ?? [])
        {
            string rawTitle = Path.GetFileNameWithoutExtension(file.Name);
            string title = Regex.Replace(
                rawTitle,
                @"^\d+((-|\s)\d+)? ",
                "");

            if (string.IsNullOrEmpty(title)) continue;
            IEnumerable<MusicBrainzRelease> matchedReleases;
            if (!string.IsNullOrEmpty(artist))
            {
                MusicBrainzRecordingAppends? brainzRecordingAppends = await musicBrainzRecordingClient.SearchRecordings($"recording:{title} artist:{artist}");
                if (brainzRecordingAppends?.Releases is null || brainzRecordingAppends.Releases.Length == 0)
                {
                    brainzRecordingAppends = await musicBrainzRecordingClient.SearchRecordings($"recording:{title}");
                    if (brainzRecordingAppends?.Releases is null || brainzRecordingAppends.Releases.Length == 0) continue;
                }
                matchedReleases = brainzRecordingAppends.Releases.Where(r => r.Id != Guid.Empty);
                musicBrainzReleases.AddRange(matchedReleases);
            }
            else
            {
                MusicBrainzRecordingAppends? brainzRecordingAppends = await musicBrainzRecordingClient.SearchRecordings($"recording:{title}");
                if (brainzRecordingAppends?.Releases is null || brainzRecordingAppends.Releases.Length == 0) continue;

                matchedReleases = brainzRecordingAppends.Releases.Where(r => r.Id != Guid.Empty);
                musicBrainzReleases.AddRange(matchedReleases);
            }
        }

        return musicBrainzReleases
            .Where(r => !string.IsNullOrEmpty(r.Title) && albumName.ContainsSanitized(r.Title))
            .Where(r => (r.DateTime?.Year ?? 0) == 0 || year == 0 || (r.DateTime?.Year ?? 0) == year)
            .DistinctBy(r => r.Id).ToArray();
    }

    private async Task<MusicBrainzRelease[]> SearchRecordingByFingerprint(
        MediaFolderExtend folder,
        string albumName)
    {
        _fromFingerprint = true;
        List<MusicBrainzRelease> musicBrainzReleases = [];

        List<MusicBrainzRelease> matchedReleases = [];
        
        foreach (MediaFile file in folder?.Files ?? [])
        {
            if (file.FFprobe is null) continue;

            matchedReleases = await Fingerprint(file, albumName, matchedReleases);
            musicBrainzReleases.AddRange(matchedReleases);
        }
        matchedReleases.Clear();

        return musicBrainzReleases.Where(r => !string.IsNullOrEmpty(r.Title) && albumName.ContainsSanitized(r.Title)).DistinctBy(r => r.Id).ToArray();
    }

    private async Task<List<MusicBrainzRelease>> Fingerprint(
        MediaFile file,
        string albumName,
        List<MusicBrainzRelease> matchedReleases,
        int retry = 0)
    {
        AcoustIdFingerprint? fingerprint = null;

        try
        {
            fingerprint = await AcoustIdFingerprintLookUp(file.Path);
        }
        catch (Exception e)
        {
            if (retry == 3)
            {
                Logger.Fingerprint(e.Message, LogEventLevel.Error);
                return matchedReleases;
            }
            await Task.Delay(200);
            retry += 1;
            return await Fingerprint(file, albumName, matchedReleases, retry);
        }

        if (fingerprint is null)
        {
            Logger.Fingerprint("No fingerprint found for: " + file.Path);
            return matchedReleases;
        }

        foreach (AcoustIdFingerprintResult fingerprintResult in fingerprint?.Results ?? [])
        {
            if (fingerprintResult.Id == Guid.Empty) continue;

            foreach (AcoustIdFingerprintRecording? fingerprintResultRecording in fingerprintResult?.Recordings ?? [])
            {
                if (fingerprintResultRecording?.Releases is null) continue;
                if (fingerprintResultRecording.Id == Guid.Empty) continue;

                AcoustIdFingerprintReleaseGroups[] fingerprintReleases = fingerprintResultRecording.Releases ?? [];
                foreach (AcoustIdFingerprintReleaseGroups fingerprintRelease in fingerprintReleases)
                {
                    if (
                        fingerprintRelease.Id == Guid.Empty
                        || fingerprintRelease.Title is null
                        || !fingerprintRelease.Title.EqualsSanitized(albumName)
                        || matchedReleases.Any(r => r.Id == fingerprintRelease.Id)
                    ) continue;

                    matchedReleases.Add(new()
                    {
                        Id = fingerprintRelease.Id,
                        Title = fingerprintRelease.Title ?? albumName,
                    });
                    // Logger.App("Matched Fingerprint Release: " + fingerprintRelease.Title + " - " + fingerprintRelease.Id);
                }
            }
        }

        return matchedReleases;
    }

    private async Task<AcoustIdFingerprint?> AcoustIdFingerprintLookUp(string path, int retry = 0)
    {
        AcoustIdFingerprintClient acoustIdFingerprintClient = new();
        AcoustIdFingerprint? result = null;
        try
        {
            result = await acoustIdFingerprintClient.Lookup(path);
        }
        catch (Exception e)
        {
            if (retry == 3)
            {
                Logger.Fingerprint(e.Message, LogEventLevel.Error);
                return null;
            }
            await Task.Delay(200);
            retry += 1;
            return await AcoustIdFingerprintLookUp(path, retry);
        }
        return result;
    }

    private async Task<MusicBrainzRelease[]> SearchReleaseByQuery(
        MusicBrainzReleaseClient musicBrainzReleaseClient,
        string albumName,
        string artistName,
        int year)
    {
        MusicBrainzRelease[] releases;
        MusicBrainzReleaseSearchResponse? result = null;
        List<MusicBrainzRelease> musicBrainzReleases = [];
        string query = $"release:{albumName}";

        if (!string.IsNullOrEmpty(artistName) && year > 0)
        {
            result = await musicBrainzReleaseClient.SearchReleases($"{query} artist:{artistName} date:{year}");
            if (result?.Releases != null)
            {
                releases = result.Releases.Where(r => r.Title.ContainsSanitized(albumName) || r.Score > 75)
                    .DistinctBy(r => r.Id).ToArray();
                musicBrainzReleases.AddRange(releases);
            }
        }

        if (musicBrainzReleases.Count == 0 && !string.IsNullOrEmpty(artistName))
        {
            result = await musicBrainzReleaseClient.SearchReleases($"{query} artist:{artistName}");
            if (result?.Releases != null)
            {
                releases = result.Releases.Where(r => r.Title.ContainsSanitized(albumName) || r.Score > 75).ToArray();
                musicBrainzReleases.AddRange(releases.DistinctBy(r => r.Id));
            }
        }

        if (musicBrainzReleases.Count == 0)
        {
            result = await musicBrainzReleaseClient.SearchReleases(query);
            if (result?.Releases != null)
            {
                releases = result.Releases.Where(r => r.Title.ContainsSanitized(albumName) || r.Score > 75).ToArray();
                musicBrainzReleases.AddRange(releases.DistinctBy(r => r.Id));
            }
        }

        MusicBrainzRelease[] matchedReleases = musicBrainzReleases
            .Where(r => r.Title.ContainsSanitized(albumName))
            .Where(r => (r.DateTime?.Year ?? 0) == 0 || year == 0 || (r.DateTime?.Year ?? 0) == year)
            .DistinctBy(r => r.Id).ToArray();
        musicBrainzReleases.Clear();
        return matchedReleases;
    }

    [GeneratedRegex(@"(?<library_folder>.+?)[\\\/]((?<letter>.{1})?|\[(?<type>.+?)\])[\\\/](?<artist>.+?)?[\\\/]?(\[(?<year>\d{4})\]|\[(?<releaseType>Singles)\])\s?(?<album>.*)?")]
    private static partial Regex PathRegex();
}