using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record GenreResponseDto
{
    [JsonProperty("data")] public GenreResponseItemDto? Data { get; set; }
}