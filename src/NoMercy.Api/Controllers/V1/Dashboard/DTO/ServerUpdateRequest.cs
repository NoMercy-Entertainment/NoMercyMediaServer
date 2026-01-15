using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record ServerUpdateRequest
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}