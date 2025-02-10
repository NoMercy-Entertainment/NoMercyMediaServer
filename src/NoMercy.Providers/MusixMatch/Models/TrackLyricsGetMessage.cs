using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class TrackLyricsGetMessage
{
    [JsonProperty("header")] public TrackLyricsGetMessageHeader Header { get; set; } = new();
    [JsonProperty("body")] public TrackLyricsGetMessagedBody? Body { get; set; }
}