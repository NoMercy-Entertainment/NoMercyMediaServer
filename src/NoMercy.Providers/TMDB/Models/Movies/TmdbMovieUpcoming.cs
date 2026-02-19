using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieUpcoming : TmdbPaginatedResponse<TmdbMovie>
{
    [JsonProperty("dates")] public TmdbUpcomingMovieDates MovieDates { get; set; } = new();
}