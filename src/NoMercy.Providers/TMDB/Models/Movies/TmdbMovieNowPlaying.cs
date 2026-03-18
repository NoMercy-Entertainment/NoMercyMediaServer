using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieNowPlaying : TmdbPaginatedResponse<TmdbMovie>
{
    [JsonProperty("dates")] public TmdbDates TmdbDates { get; set; } = new();
}