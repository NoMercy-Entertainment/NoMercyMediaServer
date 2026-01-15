using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class TrackSubtitlesGetMessageHeader
{
    [JsonProperty("status_code")] public long StatusCode { get; set; }
    [JsonProperty("available")] public long Available { get; set; }
    [JsonProperty("execute_time")] public double ExecuteTime { get; set; }
    [JsonProperty("instrumental")] public long Instrumental { get; set; }
}