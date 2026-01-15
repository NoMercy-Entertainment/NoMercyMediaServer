using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record RatingDto
{
    [JsonProperty("rating")] public string RatingRating { get; set; } = string.Empty;
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
}