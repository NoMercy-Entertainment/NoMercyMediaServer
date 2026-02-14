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
    [JsonProperty("auto_start")] public bool AutoStart { get; set; }
    [JsonProperty("update_available")] public bool UpdateAvailable { get; set; }
    [JsonProperty("latest_version")] public string? LatestVersion { get; set; }
    [JsonProperty("app_status")] public AppStatusInfo? AppStatus { get; set; }
}

public class AppStatusInfo
{
    [JsonProperty("running")] public bool Running { get; set; }
    [JsonProperty("pid")] public int? Pid { get; set; }
}
