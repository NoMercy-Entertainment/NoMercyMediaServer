using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record ArtistResponseDto
{
    [JsonProperty("data")] public ArtistResponseItemDto? Data { get; set; }
}