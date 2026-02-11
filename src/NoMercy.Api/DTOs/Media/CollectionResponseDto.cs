using Newtonsoft.Json;


namespace NoMercy.Api.DTOs.Media;

public record CollectionResponseDto
{
    [JsonProperty("nextId")] public object NextId { get; set; } = null!;
    [JsonProperty("data")] public CollectionResponseItemDto? Data { get; set; }
}