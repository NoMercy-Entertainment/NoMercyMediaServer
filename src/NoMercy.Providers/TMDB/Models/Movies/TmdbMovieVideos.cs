using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieVideos
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbMovieVideo[] Results { get; set; } = [];
}