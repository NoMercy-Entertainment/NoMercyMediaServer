using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvAlternativeTitles
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbTvAlternativeTitle[] Results { get; set; } = [];
}