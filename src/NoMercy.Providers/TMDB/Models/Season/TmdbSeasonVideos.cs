using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonVideos
{
    [JsonProperty("results")] public TmdbSeasonVideoResult[] Results { get; set; } = [];
}