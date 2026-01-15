using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.WatchProviders;

public class TmdbTvProviderResult
{
    [JsonProperty("display_priority")] public int DisplayPriority { get; set; }
    [JsonProperty("logo_path")] public string? LogoPath { get; set; }
    [JsonProperty("provider_name")] public string ProviderName { get; set; } = string.Empty;
    [JsonProperty("provider_id")] public int ProviderId { get; set; }
}