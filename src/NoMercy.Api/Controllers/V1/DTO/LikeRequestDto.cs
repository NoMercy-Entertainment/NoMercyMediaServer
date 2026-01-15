using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record LikeRequestDto
{
    [JsonProperty("value")] public bool Value { get; set; }
}