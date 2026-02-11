using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Common;

public record ContentRating
{
    [JsonProperty("rating")] public string? Rating { get; set; }
    [JsonProperty("iso_3166_1")] public string? Iso31661 { get; set; }
}