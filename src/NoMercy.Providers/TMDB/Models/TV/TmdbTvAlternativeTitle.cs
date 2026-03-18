using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvAlternativeTitle
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
}