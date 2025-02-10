using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MusixMatchSubtitleGetMessage
{
    [JsonProperty("header")] public MusixMatchSubtitleGetMessageHeader Header { get; set; } = new();
    [JsonProperty("body")] public MusixMatchSubtitleGetMessageBody? Body { get; set; }
}