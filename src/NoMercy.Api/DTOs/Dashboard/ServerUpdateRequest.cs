using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerUpdateRequest
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}