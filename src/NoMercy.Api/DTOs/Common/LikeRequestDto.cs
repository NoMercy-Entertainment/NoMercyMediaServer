using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Common;

public record LikeRequestDto
{
    [JsonProperty("value")] public bool Value { get; set; }
}