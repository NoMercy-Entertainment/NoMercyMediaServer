using System.Collections.Concurrent;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class AudioImportJob : AbstractMusicFolderJob
{
    public override string QueueName => "queue";
    public override int Priority => 3;
    public string Status { get; set; } = "pending";

    private List<MediaFile> _files = []; 
    
    public override async Task Handle()
    {
        await using MediaContext context = new();
        
        using MusicBrainzReleaseClient musicBrainzReleaseClient = new();
        using MusicBrainzRecordingClient musicBrainzRecordingClient = new();
        using MusicBrainzArtistClient musicBrainzArtistClient = new();

        _files = await GetFiles();
        
        Dictionary<Guid, List<AudioTagModel>> tags = await GetTags();

        MusicBrainzReleaseAppends? release = null;
        List<MusicBrainzArtistAppends> releaseArtists = [];
        List<(MusicBrainzRecordingAppends, MusicBrainzArtistAppends[], MediaFile)> fileLinksToTrack = [];
        
        KeyValuePair<Guid, List<AudioTagModel>> firstRelease = tags
            .OrderByDescending(x => x.Value.Count)
            .FirstOrDefault();
        
        if (firstRelease.Key != Guid.Empty)
        {
            // Release
            release = await musicBrainzReleaseClient.WithAllAppends(firstRelease.Key);
            if (release == null)
            {
                // check fingerprint or acoustid?
                return;
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
            await File.WriteAllTextAsync(@"C:\test.json", JsonConvert.SerializeObject(((release, releaseArtists), fileLinksToTrack), Formatting.Indented));
        }
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