using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class TrackSubtitlesGetMessage
{
    [JsonProperty("header")] public TrackSubtitlesGetMessageHeader Header { get; set; } = new();
    [JsonProperty("body")] public TrackSubtitlesGetMessageBody? Body { get; set; }
}