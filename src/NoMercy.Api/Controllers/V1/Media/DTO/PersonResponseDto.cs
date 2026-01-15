using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record PersonResponseDto
{
    [JsonProperty("nextId")] public long NextId { get; set; }
    [JsonProperty("data")] public PersonResponseItemDto? Data { get; set; }
}