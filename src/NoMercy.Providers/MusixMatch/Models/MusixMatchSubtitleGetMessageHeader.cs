using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MusixMatchSubtitleGetMessageHeader
{
    [JsonProperty("status_code")] public long StatusCode { get; set; }
    [JsonProperty("execute_time")] public double ExecuteTime { get; set; }
    [JsonProperty("pid")] public long Pid { get; set; }
    [JsonProperty("surrogate_key_list")] public object[] SurrogateKeyList { get; set; } = [];
}