using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record ArtistsResponseDto
{
    [JsonProperty("data")] public IEnumerable<ArtistsResponseItemDto> Data { get; set; } = [];
}