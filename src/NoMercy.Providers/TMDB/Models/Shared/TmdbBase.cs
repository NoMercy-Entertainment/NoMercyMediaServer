using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbBase
{
    [JsonProperty("backdrop_path")] public string? BackdropPath { get; set; } = string.Empty;
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("original_language")] public string OriginalLanguage { get; set; } = string.Empty;
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("popularity")] public double Popularity { get; set; }
    [JsonProperty("poster_path")] public string? PosterPath { get; set; }
    [JsonProperty("vote_average")] public double VoteAverage { get; set; }
    [JsonProperty("vote_count")] public int VoteCount { get; set; }
}