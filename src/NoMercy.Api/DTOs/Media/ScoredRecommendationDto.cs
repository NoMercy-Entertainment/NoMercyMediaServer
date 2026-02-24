using Newtonsoft.Json;
using NoMercy.Database;

namespace NoMercy.Api.DTOs.Media;

public record ScoredRecommendationDto
{
    [JsonProperty("media_id")] public int MediaId { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
    [JsonProperty("score")] public double Score { get; set; }
    [JsonProperty("source_count")] public int SourceCount { get; set; }
    [JsonProperty("link")] public Uri Link => new($"/{(MediaType != "movie" ? "tv" : "movie" )}/{MediaId}", UriKind.Relative);
}
