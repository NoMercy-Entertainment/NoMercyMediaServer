using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class TrackLyricsGetMessageHeader
{
    [JsonProperty("status_code")] public long StatusCode { get; set; }
    [JsonProperty("execute_time")] public double ExecuteTime { get; set; }
}