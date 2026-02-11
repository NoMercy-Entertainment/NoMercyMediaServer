using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record PlaylistResponseDto
{
    [JsonProperty("data")] public PlaylistResponseItemDto? Data { get; set; }
}