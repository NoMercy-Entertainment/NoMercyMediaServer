using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record CarouselResponseDto<T>
{
    [JsonProperty("data")] public IEnumerable<T> Data { get; set; } = [];
}