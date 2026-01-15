using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record AlbumResponseDto
{
    [JsonProperty("data")] public AlbumResponseItemDto? Data { get; set; }
}