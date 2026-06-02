using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record PeopleResponseDto
{
    [JsonProperty("nextId")]
    public long? NextId { get; set; }

    [JsonProperty("data")]
    public IEnumerable<PeopleResponseItemDto> Data { get; set; } = [];
}
