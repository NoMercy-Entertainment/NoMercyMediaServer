using Newtonsoft.Json;

namespace NoMercy.Cli.Models;

internal class ResourcesResponse
{
    [JsonProperty("cpu")] public CpuInfo Cpu { get; set; } = new();
    [JsonProperty("gpu")] public List<GpuInfo> Gpu { get; set; } = [];
    [JsonProperty("memory")] public MemoryInfo Memory { get; set; } = new();
    [JsonProperty("storage")] public List<StorageInfo> Storage { get; set; } = [];
}

internal class CpuInfo
{
    [JsonProperty("total")] public double Total { get; set; }
    [JsonProperty("max")] public double Max { get; set; }
}

internal class GpuInfo
{
    [JsonProperty("core")] public double Core { get; set; }
    [JsonProperty("memory")] public double Memory { get; set; }
    [JsonProperty("encode")] public double Encode { get; set; }
    [JsonProperty("decode")] public double Decode { get; set; }
    [JsonProperty("index")] public int Index { get; set; }
}

internal class MemoryInfo
{
    [JsonProperty("available")] public double Available { get; set; }
    [JsonProperty("use")] public double Use { get; set; }
    [JsonProperty("total")] public double Total { get; set; }
    [JsonProperty("percentage")] public double Percentage { get; set; }
}

internal class StorageInfo
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("total")] public double Total { get; set; }
    [JsonProperty("available")] public double Available { get; set; }
    [JsonProperty("percentage")] public double Percentage { get; set; }
}
