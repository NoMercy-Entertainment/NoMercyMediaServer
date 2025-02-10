using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record Credits
{
    [JsonProperty("cast")] public KnownFor[] Cast { get; set; } = [];
    [JsonProperty("crew")] public KnownFor[] Crew { get; set; } = [];
}