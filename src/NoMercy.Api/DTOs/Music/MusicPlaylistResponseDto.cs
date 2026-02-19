using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public class MusicPlaylistResponseDto
{
    [JsonProperty("data")] public List<MusicPlaylistResponseItemDto> Data { get; set; } = [];
}