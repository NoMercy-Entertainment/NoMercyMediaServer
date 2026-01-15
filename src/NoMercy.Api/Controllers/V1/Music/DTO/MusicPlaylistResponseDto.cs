using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public class MusicPlaylistResponseDto
{
    [JsonProperty("data")] public List<MusicPlaylistResponseItemDto> Data { get; set; } = [];
}