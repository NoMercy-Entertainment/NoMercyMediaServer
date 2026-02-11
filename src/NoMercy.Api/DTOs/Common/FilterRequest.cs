using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Common;

public record FilterRequest
{
    [JsonProperty("letter")] public string? Letter { get; set; } = "_";
}