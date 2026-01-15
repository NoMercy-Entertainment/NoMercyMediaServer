using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record EncoderProfileDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("container")] public string? Container { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonProperty("params")] public ParamsDto? Params { get; set; }
}