using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MatcherTrackGetMessage
{
    [JsonProperty("header")] public MusixMatchMatcherTrackGetMessageHeader Header { get; set; } = new();
    [JsonProperty("body")] public MatcherTrackGetMessageBody Body { get; set; } = new();
}