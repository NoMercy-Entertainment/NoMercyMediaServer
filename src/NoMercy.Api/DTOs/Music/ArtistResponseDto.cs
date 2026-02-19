using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record ArtistResponseDto
{
    [JsonProperty("data")] public ArtistResponseItemDto? Data { get; set; }
}