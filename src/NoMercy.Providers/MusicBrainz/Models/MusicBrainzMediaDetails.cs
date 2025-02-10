using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzMediaDetails : MusicBrainzMedia
{
    [JsonProperty("discs")] public Disc[] Discs { get; set; } = [];
    [JsonProperty("track-offset")] public int TrackOffset { get; set; }
}