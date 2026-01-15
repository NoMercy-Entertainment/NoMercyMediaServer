using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record DataResponseDto<T>
{
    [JsonProperty("data")] public T? Data { get; set; }
}