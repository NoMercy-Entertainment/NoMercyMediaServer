using Newtonsoft.Json;
using NoMercy.Helpers.Monitoring;

namespace NoMercy.Api.DTOs.Dashboard;

public record ResourceInfoDto
{
    [JsonProperty("cpu")] public Cpu Cpu { get; set; } = new();
    [JsonProperty("gpu")] public List<Gpu> Gpu { get; set; } = new();
    [JsonProperty("memory")] public Memory Memory { get; set; } = new();
    [JsonProperty("storage")] public List<ResourceMonitorDto> Storage { get; set; } = new();
}