using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record PlaylistResponseDto
{
    [JsonProperty("data")] public PlaylistResponseItemDto? Data { get; set; }
}