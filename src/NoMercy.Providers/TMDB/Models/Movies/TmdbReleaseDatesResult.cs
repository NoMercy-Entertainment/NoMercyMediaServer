using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbReleaseDatesResult
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("release_dates")] public TmdbReleaseDate[] ReleaseDates { get; set; } = [];
}