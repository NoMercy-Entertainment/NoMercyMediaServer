using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbWatchProviders
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbWatchProviderResults TmdbWatchProviderResults { get; set; } = new();
}