using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Api.DTOs.Dashboard;

public record FolderDriverDto
{
    [JsonProperty("driver_type")]
    public string DriverType { get; set; } = "local";

    [JsonProperty("driver_config")]
    public JObject? DriverConfig { get; set; }
}

public record FolderDriverResponseDto
{
    [JsonProperty("driver_type")]
    public string DriverType { get; set; } = "local";

    [JsonProperty("driver_config")]
    public JObject? DriverConfig { get; set; }

    [JsonProperty("warnings")]
    public string[] Warnings { get; set; } = [];
}

public record UpdateFolderDriverRequestDto
{
    [JsonProperty("driver_type")]
    public string DriverType { get; set; } = string.Empty;

    [JsonProperty("driver_config")]
    public JObject? DriverConfig { get; set; }
}

public record DriverMetadataDto
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("available")]
    public bool Available { get; set; }

    [JsonProperty("config_schema")]
    public Dictionary<string, string> ConfigSchema { get; set; } = new();
}
