using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.MediaProcessing.Recordings;
using NoMercy.Providers.NoMercy.Client;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class TrackLyricsJob : AbstractLyricJob
{
    public override string QueueName => "queue";
    public override int Priority => 0;
    public override async Task Handle()
    {
        await Task.Delay(1000); // wait for 
        await using MediaContext mediaContext = new();
        RecordingRepository recordingRepository = new(mediaContext);
        dynamic? subtitles = await NoMercyLyricsClient.SearchLyrics(Track);
        if (subtitles is null) return;
        await recordingRepository.UpdateTrackLyricsAsync(Track, JsonConvert.SerializeObject(subtitles));
    }
}