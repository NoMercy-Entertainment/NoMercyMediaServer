using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record AlbumResponseDto
{
    [JsonProperty("data")] public AlbumResponseItemDto? Data { get; set; }
}