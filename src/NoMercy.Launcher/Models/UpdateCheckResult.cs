using Newtonsoft.Json;

namespace NoMercy.Launcher.Models;

public class UpdateCheckResult
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("use_installer")]
    public bool UseInstaller { get; set; }

    [JsonProperty("latest_version")]
    public string? LatestVersion { get; set; }

    [JsonProperty("path")]
    public string? Path { get; set; }
}
