using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record AvailableResponseDto
{
    [JsonProperty("available")] public bool Available { get; set; }
    [JsonProperty("server")] public string? Message { get; set; }
}