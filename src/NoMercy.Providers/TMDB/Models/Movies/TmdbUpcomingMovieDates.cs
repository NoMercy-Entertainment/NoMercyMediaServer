using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbUpcomingMovieDates
{
    [JsonProperty("maximum")] public DateTime? Maximum { get; set; }
    [JsonProperty("minimum")] public DateTime? Minimum { get; set; }
}