using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MusixMatchMatcherTrackGetMessageHeader
{
    [JsonProperty("status_code")] public long StatusCode { get; set; }
    [JsonProperty("execute_time")] public double ExecuteTime { get; set; }
    [JsonProperty("confidence")] public long Confidence { get; set; }
    [JsonProperty("mode")] public string Mode { get; set; } = string.Empty;
    [JsonProperty("cached")] public long Cached { get; set; }
}