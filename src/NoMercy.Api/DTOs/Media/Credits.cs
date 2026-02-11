using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record Credits
{
    [JsonProperty("cast")] public KnownForDto[] Cast { get; set; } = [];
    [JsonProperty("crew")] public KnownForDto[] Crew { get; set; } = [];
}