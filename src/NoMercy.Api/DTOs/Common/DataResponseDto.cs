using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Common;

public record DataResponseDto<T>
{
    [JsonProperty("data")] public T? Data { get; set; }
}