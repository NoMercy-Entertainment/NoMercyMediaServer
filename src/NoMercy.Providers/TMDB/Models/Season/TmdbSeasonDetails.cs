using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Episode;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonDetails : TmdbSeason
{
    [JsonProperty("episodes")] public TmdbEpisodeDetails[] Episodes { get; set; } = [];
}