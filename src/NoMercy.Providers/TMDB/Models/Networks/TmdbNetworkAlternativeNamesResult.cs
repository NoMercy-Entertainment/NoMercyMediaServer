using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Networks;

public class TmdbNetworkAlternativeNamesResult
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}