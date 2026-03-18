using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record AlbumsResponseDto
{
    [JsonProperty("data")] public IEnumerable<AlbumsResponseItemDto> Data { get; set; } = [];
}