using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackList
{
    [JsonProperty("track")] public MusixMatchMusixMatchTrack MusixMatchMusixMatchTrack { get; set; } = new();
}
