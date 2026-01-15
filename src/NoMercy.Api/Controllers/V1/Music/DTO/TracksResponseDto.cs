using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record TracksResponseDto
{
    [JsonProperty("data")] public TracksResponseItemDto Data { get; set; } = new();
}