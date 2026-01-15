using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record CarouselResponseDto<T>
{
    [JsonProperty("data")] public IEnumerable<T> Data { get; set; } = [];
}