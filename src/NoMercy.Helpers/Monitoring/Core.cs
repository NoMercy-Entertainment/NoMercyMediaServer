using Newtonsoft.Json;

namespace NoMercy.Helpers.Monitoring;

public class Core
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("utilization")] public double Utilization { get; set; }
}