using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbRecommendationsTvShow : TmdbTvShow
{
    [JsonProperty("adult")] public bool Adult { get; set; }
}