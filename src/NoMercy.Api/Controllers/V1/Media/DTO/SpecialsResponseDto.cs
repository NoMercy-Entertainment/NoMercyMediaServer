using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record SpecialsResponseDto
{
    [JsonProperty("nextId")] public object NextId { get; set; } = null!;
    [JsonProperty("data")] public IEnumerable<SpecialsResponseItemDto> Data { get; set; } = [];

}
