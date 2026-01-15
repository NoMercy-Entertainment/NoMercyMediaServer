using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record ArtistsResponseDto
{
    [JsonProperty("data")] public IEnumerable<ArtistsResponseItemDto> Data { get; set; } = [];
}