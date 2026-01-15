using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record InfoResponseDto
{
    [JsonProperty("data")] public InfoResponseItemDto? Data { get; set; }
}