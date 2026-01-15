using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record FilterRequest
{
    [JsonProperty("letter")] public string? Letter { get; set; } = "_";
}