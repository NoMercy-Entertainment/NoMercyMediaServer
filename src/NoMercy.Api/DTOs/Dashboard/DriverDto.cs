using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Api.DTOs.Dashboard;

public record DriverDto
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("config")]
    public JObject? Config { get; set; }

    [JsonProperty("credentials_configured")]
    public bool CredentialsConfigured { get; set; }

    [JsonProperty("folder_count")]
    public int FolderCount { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public record CreateDriverRequestDto
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("config")]
    public JObject? Config { get; set; }

    [JsonProperty("credentials")]
    public DriverCredentialsDto? Credentials { get; set; }
}

public record UpdateDriverRequestDto
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("config")]
    public JObject? Config { get; set; }

    [JsonProperty("credentials")]
    public DriverCredentialsDto? Credentials { get; set; }
}

public record DriverCredentialsDto
{
    [JsonProperty("access_key")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonProperty("secret_key")]
    public string SecretKey { get; set; } = string.Empty;
}

public record FolderDriverAssignDto
{
    [JsonProperty("driver_id")]
    public string? DriverId { get; set; }

    /// <summary>
    /// Optional sub-path within the driver root. When null the existing
    /// folder path is preserved; when non-null (including empty string)
    /// it replaces the folder path.
    /// </summary>
    [JsonProperty("path")]
    public string? Path { get; set; }
}

public record FolderDriverInfoDto
{
    [JsonProperty("driver_id")]
    public string? DriverId { get; set; }

    [JsonProperty("driver_name")]
    public string? DriverName { get; set; }

    [JsonProperty("driver_type")]
    public string? DriverType { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;
}
