using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieAlternativeTitles
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("titles")] public TmdbMovieAlternativeTitle[] Results { get; set; } = [];
}