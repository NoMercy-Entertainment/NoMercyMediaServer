using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerInfoDto
{
    [JsonProperty("server")] public string Server { get; set; } = string.Empty;
    [JsonProperty("cpu")] public List<string> Cpu { get; set; } = [];
    [JsonProperty("gpu")] public List<string> Gpu { get; set; } = [];
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;
    [JsonProperty("arch")] public string Arch { get; set; } = string.Empty;
    [JsonProperty("version")] public string? Version { get; set; }
    [JsonProperty("bootTime")] public DateTime BootTime { get; set; }
    [JsonProperty("os_version")] public string? OsVersion { get; set; }
    [JsonProperty("setup_complete")] public bool SetupComplete { get; set; }
}