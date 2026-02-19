using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieReleaseDates
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbReleaseDatesResult[] Results { get; set; } = [];
}