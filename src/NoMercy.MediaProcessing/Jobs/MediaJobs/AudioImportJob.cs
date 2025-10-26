using System.Collections.Concurrent;
using NoMercy.Database;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
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

        _files = await GetFiles();
        
        Dictionary<Guid, List<AudioTagModel>> tags = await GetTags();
        
        KeyValuePair<Guid, List<AudioTagModel>> firstRelease = tags
            .OrderByDescending(x => x.Value.Count)
            .FirstOrDefault();
        
        if (firstRelease.Key != Guid.Empty)
        {
            MusicBrainzReleaseAppends? result = await musicBrainzReleaseClient.WithAllAppends(firstRelease.Key);
            if (result == null)
            {
                Logger.Queue($"MusicBrainz release {firstRelease.Key} not found.");
                // do something else
                return;
            }
            
            Logger.Queue(firstRelease.Value);
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