using LibreHardwareMonitor.Hardware;
using Newtonsoft.Json;

namespace NoMercy.Helpers.Monitoring;

public class Gpu
{
    [JsonProperty("d3d")] public double D3D { get; set; }
    [JsonProperty("decode")] public double Decode { get; set; }
    [JsonProperty("core")] public double Core { get; set; }
    [JsonProperty("memory")] public double Memory { get; set; }
    [JsonProperty("encode")] public double Encode { get; set; }
    [JsonProperty("power")] public double Power { get; set; }
    [JsonProperty("identifier")] internal Identifier Identifier { get; set; } = new();
    [JsonProperty("index")] public int Index => int.Parse(Identifier.ToString().Split('/').LastOrDefault() ?? "0");
}