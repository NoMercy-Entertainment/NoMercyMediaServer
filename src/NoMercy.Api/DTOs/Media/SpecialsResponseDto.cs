using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record SpecialsResponseDto
{
    [JsonProperty("nextId")] public object NextId { get; set; } = null!;
    [JsonProperty("data")] public IEnumerable<SpecialsResponseItemDto> Data { get; set; } = [];
}