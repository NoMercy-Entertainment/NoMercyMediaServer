using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record AlbumsResponseDto
{
    [JsonProperty("data")] public IEnumerable<AlbumsResponseItemDto> Data { get; set; } = [];
}