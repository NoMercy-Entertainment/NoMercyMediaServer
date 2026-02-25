using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Database;

namespace NoMercy.Api.DTOs.Media;

public record RecommendationDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("link")] public Uri Link => new($"/dashboard/recommendations/{(Type != "movie" ? "tv" : "movie")}/{Id}", UriKind.Relative);

    // Internal properties used for scoring/diversity — not serialized to JSON
    [JsonIgnore] public string? TitleSort { get; set; }
    [JsonIgnore] public string? Backdrop { get; set; }
    [JsonIgnore] public double Score { get; set; }
    [JsonIgnore] public int SourceCount { get; set; }
    [JsonIgnore] public List<int> SourceIds { get; set; } = [];
}

public record RecommendationDetailDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("voteAverage")] public double? VoteAverage { get; set; }
    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; } = [];
    [JsonProperty("content_ratings")] public IEnumerable<ContentRating> ContentRatings { get; set; } = [];
    [JsonProperty("external_ids")] public ExternalIds? ExternalIds { get; set; }
    [JsonProperty("because_you_have")] public List<RecommendationDetailSourceDto> BecauseYouHave { get; set; } = [];
    [JsonProperty("link")] public Uri Link => new($"/{MediaType}/{Id}", UriKind.Relative);
}

public record RecommendationDetailSourceDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("link")] public Uri Link => new($"/{MediaType}/{Id}", UriKind.Relative);
    [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
    [JsonProperty("have_items")] public int HaveItems { get; set; }
    [JsonProperty("number_of_items")] public int NumberOfItems { get; set; }
    [JsonProperty("duration")] public int? Duration { get; set; }
    [JsonProperty("tags")] public IEnumerable<string> Tags { get; set; } = [];
}
