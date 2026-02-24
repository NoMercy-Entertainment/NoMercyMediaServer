using Newtonsoft.Json;
using NoMercy.Database;

namespace NoMercy.Api.DTOs.Media;

public record RecommendationSourceDto
{
    [JsonProperty("id")] public int Id { get; init; }
    [JsonProperty("title")] public string Title { get; init; } = string.Empty;
    [JsonProperty("poster")] public string? Poster { get; init; }
    [JsonProperty("type")] public string Type { get; init; } = string.Empty;
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; init; }
    [JsonProperty("link")] public Uri Link => new($"/{(Type != "movie" ? "tv" : "movie")}/{Id}", UriKind.Relative);
}

public record RecommendationDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("score")] public double Score { get; set; }
    [JsonProperty("source_count")] public int SourceCount { get; set; }
    [JsonProperty("because_you_have")] public List<RecommendationSourceDto> BecauseYouHave { get; set; } = [];
    [JsonProperty("link")] public Uri Link => new($"/{(Type != "movie" ? "tv" : "movie")}/{Id}", UriKind.Relative);
}
