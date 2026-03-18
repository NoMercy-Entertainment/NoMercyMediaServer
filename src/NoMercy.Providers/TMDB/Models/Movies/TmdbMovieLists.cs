using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieLists : TmdbPaginatedResponse<TmdbMovie>
{
    [JsonProperty("id")] public int Id { get; set; }
}