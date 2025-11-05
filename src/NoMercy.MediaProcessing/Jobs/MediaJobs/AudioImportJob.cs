using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Artists;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.MediaProcessing.MusicGenres;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.MediaProcessing.ReleaseGroups;
using NoMercy.MediaProcessing.Releases;
using NoMercy.Networking.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class AudioImportJob : AbstractMusicFolderJob
{
    public override string QueueName => "queue";
    public override int Priority => 3;
    

    private List<MediaFile> _files = []; 
    
    public override async Task Handle()
    {
        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        using MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        using MusicBrainzArtistClient musicBrainzArtistClient = new();
        
        _files = await GetFiles();
        
        Dictionary<Guid, List<AudioTagModel>> tags = await GetTags();

        MusicBrainzReleaseAppends? release = null;
        List<MusicBrainzArtistAppends> releaseArtists = [];
        List<(MusicBrainzRecordingAppends, MusicBrainzArtistAppends[], MediaFile)> fileLinksToTrack = [];
        
        if (InputFolder.Contains("[Singles]"))
        {
            foreach (KeyValuePair<Guid, List<AudioTagModel>> tag in tags)
            {
                release = await FetchInfo(tag, musicBrainzReleaseClient, musicBrainzArtistClient, releaseArtists,
                    musicBrainzRecordingClient, fileLinksToTrack, release);
                if (release == null) continue;
                await UpdateOrCreate(release, releaseArtists, fileLinksToTrack);
            }
        }
        else
        {
            KeyValuePair<Guid, List<AudioTagModel>> firstRelease = tags
                .OrderByDescending(x => x.Value.Count)
                .FirstOrDefault();

            release = await FetchInfo(firstRelease, musicBrainzReleaseClient, musicBrainzArtistClient, releaseArtists,
                musicBrainzRecordingClient, fileLinksToTrack, release);
            if (release == null) return;
            await UpdateOrCreate(release, releaseArtists, fileLinksToTrack);
        }
    }

    private static async Task<MusicBrainzReleaseAppends?> FetchInfo(KeyValuePair<Guid, List<AudioTagModel>> firstRelease, MusicBrainzReleaseClient musicBrainzReleaseClient,
        MusicBrainzArtistClient musicBrainzArtistClient, List<MusicBrainzArtistAppends> releaseArtists,
        MusicBrainzRecordingClient musicBrainzRecordingClient, List<(MusicBrainzRecordingAppends, MusicBrainzArtistAppends[], MediaFile)> fileLinksToTrack, MusicBrainzReleaseAppends? release)
    {
        if (firstRelease.Key == Guid.Empty) return release;
        
        // Release
        release = await musicBrainzReleaseClient.WithAllAppends(firstRelease.Key);
        if (release == null)
        {
            // check fingerprint or acoustid?
            return release;
        }
        
        // Release Artists
        foreach (ReleaseArtistCredit musicBrainzArtistCredit in release.ArtistCredit)
        {
            MusicBrainzArtistAppends? artist = await musicBrainzArtistClient.WithAllAppends(musicBrainzArtistCredit.MusicBrainzArtist.Id);
            if (artist == null) continue;
            releaseArtists.Add(artist);
        }
        
        // Tracks
        List<MusicBrainzTrack> allTracks = release.Media.SelectMany(x => x.Tracks).ToList();
        List<string> checkedFiles = [];
        foreach (MusicBrainzTrack track in allTracks)
        {
            foreach (AudioTagModel audioTagModel in firstRelease.Value)
            {
                if (audioTagModel.musicBrainz == null || audioTagModel.tags == null || checkedFiles.Contains(audioTagModel.fileItem.Path)) continue;
                if ((audioTagModel.musicBrainz.RecordingId != track.Recording.Id &&
                     audioTagModel.musicBrainz.RecordingId != track.Id &&
                     audioTagModel.musicBrainz.ReleaseTrackId != track.Recording.Id &&
                     audioTagModel.musicBrainz.ReleaseTrackId != track.Id) &&
                    (!audioTagModel.tags.Title.ContainsSanitized(track.Title) ||
                     (audioTagModel.tags.Track != track.Position &&
                      !(Math.Abs(float.Parse(audioTagModel.format?.Duration ?? "0") * 1000 - track.Length ??
                                 0) < 5)))) continue;
                // Recording
                MusicBrainzRecordingAppends? recording = await musicBrainzRecordingClient.WithAllAppends(track.Recording.Id);
                if (recording == null) continue;
                
                // Recording Artists
                List<MusicBrainzArtistAppends> artists = [];
                foreach (MusicBrainzArtistCredit musicBrainzArtistCredit in recording.ArtistCredit)
                {
                    MusicBrainzArtistAppends? subArtist = await musicBrainzArtistClient.WithAllAppends(musicBrainzArtistCredit.MusicBrainzArtist.Id);
                    if (subArtist == null) continue;
                    artists.Add(subArtist);
                }
                
                // Link file to track
                checkedFiles.Add(audioTagModel.fileItem.Path);
                fileLinksToTrack.Add((recording, artists.ToArray(), audioTagModel.fileItem));
                break;
            }
        }

        return release;
    }

    private async Task UpdateOrCreate(MusicBrainzReleaseAppends releaseAppends, List<MusicBrainzArtistAppends> releaseArtists, List<(MusicBrainzRecordingAppends, MusicBrainzArtistAppends[], MediaFile)> fileLinksToTrack)
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();
        
        ReleaseGroupRepository releaseGroupRepository = new(context);
        ReleaseGroupManager releaseGroupManager = new(releaseGroupRepository);

        MusicGenreRepository musicGenreRepository = new(context);

        ReleaseRepository releaseRepository = new(context);
        ReleaseManager releaseManager = new(releaseRepository, musicGenreRepository, jobDispatcher);

        ArtistRepository artistRepository = new(context);
        ArtistManager artistManager = new(artistRepository, musicGenreRepository, jobDispatcher);

        RecordingRepository recordingRepository = new(context);
        RecordingManager recordingManager = new(recordingRepository, musicGenreRepository, artistRepository);

        Library albumLibrary = await context.Libraries
            .Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
            .ThenInclude(f => f.Folder)
            .FirstAsync();
        
        Folder folderLibrary = albumLibrary.FolderLibraries.First().Folder;
        
        Logger.App($"Processing release: {releaseAppends.Title} with id: {releaseAppends.Id}", LogEventLevel.Debug);
        
        CoverArtImageManagerManager.CoverPalette? coverPalette = await CoverArtImageManagerManager.Add(releaseAppends.MusicBrainzReleaseGroup.Id, true);
        
        if (coverPalette is not null) 
            await CoverArtCoverArtClient.Download(coverPalette.Url);

        await releaseGroupManager.Store(releaseAppends.MusicBrainzReleaseGroup, LibraryId, coverPalette);
        
        await releaseManager.Store(releaseAppends, albumLibrary, folderLibrary, fileLinksToTrack.First().Item3, coverPalette);

        foreach (MusicBrainzArtistAppends artist in releaseArtists)
        {
            try
            {
                await artistManager.Store(artist, releaseAppends, albumLibrary, folderLibrary);
                jobDispatcher.DispatchJob<MusicDescriptionJob>(artist);
            }
            catch (Exception e)
            {
                Logger.MusicBrainz(e, LogEventLevel.Error);
            }
        }

        foreach ((MusicBrainzRecordingAppends recordingAppends, MusicBrainzArtistAppends[] artistAppends, MediaFile mediaFile) in fileLinksToTrack)
        {
            try
            {
                await recordingManager.Store(releaseAppends, recordingAppends, artistAppends, mediaFile, folderLibrary, coverPalette);
            }
            catch (Exception e)
            {
                Logger.MusicBrainz(e, LogEventLevel.Error);
            }
        }

        jobDispatcher.DispatchJob<MusicDescriptionJob>(releaseAppends.MusicBrainzReleaseGroup);

        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["music", "albums"]
        });
    }

    private async Task<Dictionary<Guid, List<AudioTagModel>>> GetTags()
    {
        Dictionary<Guid, List<AudioTagModel>> releasesCount =  new();
        foreach (MediaFile audioFile in _files)
        {
            AudioTagModel tags = await AudioTagModel.Create(audioFile);
            if (tags.musicBrainz == null || tags.musicBrainz.ReleaseId == Guid.Empty) continue;
            if (!releasesCount.ContainsKey(tags.musicBrainz.ReleaseId))
            {
                releasesCount.Add(tags.musicBrainz.ReleaseId, [tags]);
                continue;
            }
            
            releasesCount[tags.musicBrainz.ReleaseId].Add(tags);
        }

        return releasesCount;
    }

    private async Task<List<MediaFile>> GetFiles()
    {
        await using MediaScan mediaScan = new();
        ConcurrentBag<MediaFolderExtend> rootFolders = await mediaScan
            .DisableRegexFilter()
            .EnableFileListing()
            .Process(InputFolder, 1);
        
        return rootFolders.SelectMany(x => x.Files ?? []).ToList();
    }
}