using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record Credits
{
    [JsonProperty("cast")] public KnownForDto[] Cast { get; set; } = [];
    [JsonProperty("crew")] public KnownForDto[] Crew { get; set; } = [];
}