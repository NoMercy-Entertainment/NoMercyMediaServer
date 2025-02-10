using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record ServerInfoDto
{
    [JsonProperty("server")] public string Server { get; set; } = string.Empty;
    [JsonProperty("cpu")] public string[] Cpu { get; set; } = [];
    [JsonProperty("gpu")] public string[] Gpu { get; set; } = [];
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;
    [JsonProperty("arch")] public string Arch { get; set; } = string.Empty;
    [JsonProperty("version")] public string? Version { get; set; }
    [JsonProperty("bootTime")] public DateTime BootTime { get; set; }
    [JsonProperty("os_version")] public string? OsVersion { get; set; }
}