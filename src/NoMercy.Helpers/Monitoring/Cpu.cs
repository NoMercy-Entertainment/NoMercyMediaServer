using Newtonsoft.Json;

namespace NoMercy.Helpers.Monitoring;

public class Cpu
{
    [JsonProperty("total")] public double Total { get; set; }
    [JsonProperty("max")] public double Max { get; set; }
    [JsonProperty("core")] public List<Core> Core { get; set; } = [];
}