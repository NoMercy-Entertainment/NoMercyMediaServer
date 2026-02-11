using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record LoloMoResponseDto<T>
{
    [JsonProperty("data")] public IEnumerable<ComponentDto<NmCardDto>> Data { get; set; } = [];
}