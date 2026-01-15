using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackSubtitlesGet
{
    [JsonProperty("message")] public TrackSubtitlesGetMessage? Message { get; set; }
}