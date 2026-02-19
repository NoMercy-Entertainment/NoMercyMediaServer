using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonAggregatedCredits
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("cast")] public TmdbTmdbAggregatedCast[] Cast { get; set; } = [];
    [JsonProperty("crew")] public TmdbTmdbAggregatedCrew[] Crew { get; set; } = [];
}