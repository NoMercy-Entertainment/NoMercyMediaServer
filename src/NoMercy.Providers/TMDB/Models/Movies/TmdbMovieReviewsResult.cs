using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieReviewsResult
{
    [JsonProperty("author")] public string Author { get; set; } = string.Empty;
    [JsonProperty("author_details")] public TmdbAuthorDetails TmdbAuthorDetails { get; set; } = new();
    [JsonProperty("content")] public string Content { get; set; } = string.Empty;
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonProperty("url")] public Uri? Url { get; set; }
}