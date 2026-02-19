using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record TracksResponseDto
{
    [JsonProperty("data")] public TracksResponseItemDto Data { get; set; } = new();
}