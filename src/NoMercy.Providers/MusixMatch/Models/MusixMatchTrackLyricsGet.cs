using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackLyricsGet
{
    [JsonProperty("message")] public TrackLyricsGetMessage? Message { get; set; }
}