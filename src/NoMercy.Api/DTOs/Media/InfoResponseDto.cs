using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record InfoResponseDto
{
    [JsonProperty("data")] public InfoResponseItemDto? Data { get; set; }
}