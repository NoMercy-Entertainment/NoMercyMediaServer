using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonCredits
{
    [JsonProperty("cast")] public TmdbCast[] Cast { get; set; } = [];

    [JsonProperty("crew")] public TmdbCrew[] Crew { get; set; } = [];
}