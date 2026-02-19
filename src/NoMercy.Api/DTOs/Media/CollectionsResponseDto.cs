using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record CollectionsResponseDto
{
    [JsonProperty("data")] public IOrderedEnumerable<CollectionsResponseItemDto>? Data { get; set; }
}