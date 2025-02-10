using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class TrackSnippetGetMessageBody
{
    [JsonProperty("snippet")] public MusixMatchSnippet MusixMatchSnippet { get; set; } = new();
}