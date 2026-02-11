using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Music;

public record GenreResponseDto
{
    [JsonProperty("data")] public GenreResponseItemDto? Data { get; set; }
}