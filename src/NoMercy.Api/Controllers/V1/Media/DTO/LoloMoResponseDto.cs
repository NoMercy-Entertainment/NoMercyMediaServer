using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record LoloMoResponseDto<T>
{
    [JsonProperty("data")] public IEnumerable<LoloMoRowDto<T>> Data { get; set; } = [];
}