using Newtonsoft.Json;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ContinueWatchingDto
{
    [JsonProperty("data")] public IEnumerable<ContinueWatchingItemDto> Data { get; set; } = [];
}
