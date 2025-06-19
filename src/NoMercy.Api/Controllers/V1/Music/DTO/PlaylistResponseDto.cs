using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record PlaylistResponseDto
{
    [JsonProperty("data")] public List<PlaylistResponseItemDto> Data { get; set; } = [];
}