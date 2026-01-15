using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionPart
{
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("backdrop_path")] public object? BackdropPath { get; set; }
    [JsonProperty("genre_ids")] public int[] GenreIds { get; set; } = [];
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("original_language")] public string? OriginalLanguage { get; set; }
    [JsonProperty("original_title")] public string OriginalTitle { get; set; } = string.Empty;
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }
    [JsonProperty("poster_path")] public string? PosterPath { get; set; }
    [JsonProperty("popularity")] public double? Popularity { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("video")] public string? Video { get; set; }
    [JsonProperty("vote_average")] public double VoteAverage { get; set; }
    [JsonProperty("vote_count")] public int VoteCount { get; set; }
}