using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.WatchProviders;

public class TmdbTvProvider
{
    [JsonProperty("results")] public TmdbTvProviderResult[] Results { get; set; } = [];
}