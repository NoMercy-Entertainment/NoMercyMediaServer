using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record ContentRating
{
    [JsonProperty("rating")] public string? Rating { get; set; }
    [JsonProperty("iso_3166_1")] public string? Iso31661 { get; set; }
}