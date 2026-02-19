using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Common;

public record AvailableResponseDto
{
    [JsonProperty("available")] public bool Available { get; set; }
    [JsonProperty("server")] public string? Message { get; set; }
}