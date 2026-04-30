using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Api.DTOs.Dashboard;

public record FolderBackendDto
{
    [JsonProperty("backend_type")]
    public string BackendType { get; set; } = "local";

    [JsonProperty("backend_config")]
    public JObject? BackendConfig { get; set; }
}

public record FolderBackendResponseDto
{
    [JsonProperty("backend_type")]
    public string BackendType { get; set; } = "local";

    [JsonProperty("backend_config")]
    public JObject? BackendConfig { get; set; }

    [JsonProperty("warnings")]
    public string[] Warnings { get; set; } = [];
}

public record UpdateFolderBackendRequestDto
{
    [JsonProperty("backend_type")]
    public string BackendType { get; set; } = string.Empty;

    [JsonProperty("backend_config")]
    public JObject? BackendConfig { get; set; }
}

public record BackendTypeMetadataDto
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
