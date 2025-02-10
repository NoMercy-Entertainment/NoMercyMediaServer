using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchMatcherTrackGet
{
    [JsonProperty("message")] public MatcherTrackGetMessage Message { get; set; } = new();
}
