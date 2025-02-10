using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record RatingClass
{
    [JsonProperty("rating")] public string Rating { get; set; } = string.Empty;
    [JsonProperty("meaning")] public string Meaning { get; set; } = string.Empty;
    [JsonProperty("order")] public long Order { get; set; }
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("image")] public string Image { get; set; } = string.Empty;
}
