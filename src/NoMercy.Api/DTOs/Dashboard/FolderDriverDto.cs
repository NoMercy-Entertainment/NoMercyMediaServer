using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

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
