using Newtonsoft.Json;

namespace NoMercy.Tray.Models;

public class ServerStatusResponse
{
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("server_name")] public string ServerName { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("platform")] public string Platform { get; set; } = string.Empty;
    [JsonProperty("architecture")] public string Architecture { get; set; } = string.Empty;
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;
    [JsonProperty("uptime_seconds")] public long UptimeSeconds { get; set; }
    [JsonProperty("start_time")] public DateTime StartTime { get; set; }
    [JsonProperty("is_dev")] public bool IsDev { get; set; }
}
