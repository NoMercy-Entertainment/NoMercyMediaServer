using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvAggregatedCredits
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("cast")] public TmdbTmdbAggregatedCast[] Cast { get; set; } = [];
    [JsonProperty("crew")] public TmdbTmdbAggregatedCrew[] Crew { get; set; } = [];
}