using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record PersonResponseDto
{
    [JsonProperty("nextId")] public long NextId { get; set; }
    [JsonProperty("data")] public PersonResponseItemDto? Data { get; set; }
}