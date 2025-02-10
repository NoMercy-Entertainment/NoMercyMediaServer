using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record CollectionsResponseDto
{
    [JsonProperty("data")] public IOrderedEnumerable<CollectionsResponseItemDto>? Data { get; set; }
}