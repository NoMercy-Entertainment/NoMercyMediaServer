using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Networks;

public class TmdbNetworkAlternativeNames
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbNetworkAlternativeNamesResult[] Results { get; set; } = [];
}