using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MatcherTrackGetMessageBody
{
    [JsonProperty("track")] public MusixMatchMusixMatchTrack MusixMatchMusixMatchTrack { get; set; } = new();
}