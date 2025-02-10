using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackSnippetGet
{
    [JsonProperty("message")] public TrackSnippetGetMessage Message { get; set; } = new();
}
