using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record AniDbCredentialsResponseDto
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("username")] public string Username { get; set; } = string.Empty;
    [JsonProperty("api_key")] public string? ApiKey { get; set; }
}