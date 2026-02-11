using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerPathsDto
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("value")] public string Value { get; set; } = string.Empty;
}