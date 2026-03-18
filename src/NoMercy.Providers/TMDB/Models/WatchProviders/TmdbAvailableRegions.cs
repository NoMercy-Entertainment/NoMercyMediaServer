using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.WatchProviders;

public class TmdbAvailableRegions
{
    [JsonProperty("results")] public TmdbAvailableRegionsResult[] Results { get; set; } = [];
}