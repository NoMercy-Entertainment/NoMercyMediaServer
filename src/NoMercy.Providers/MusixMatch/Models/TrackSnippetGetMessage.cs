using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class TrackSnippetGetMessage
{
    [JsonProperty("header")] public TrackSnippetGetMessageHeader Header { get; set; } = new();
    [JsonProperty("body")] public TrackSnippetGetMessageBody Body { get; set; } = new();
}