using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbSimilarTmdbTvShow : TmdbTvShow
{
    [JsonProperty("adult")] public bool Adult { get; set; }
}