using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvWatchProviderType
{
    [JsonProperty("link")] public Uri? Link { get; set; }
    [JsonProperty("buy")] public TmdbTvWatchProviderTypeData[] Buy { get; set; } = [];
    [JsonProperty("flatrate")] public TmdbTvWatchProviderTypeData[] Flatrate { get; set; } = [];
    [JsonProperty("ads")] public TmdbTvWatchProviderTypeData[] Ads { get; set; } = [];
    [JsonProperty("rent")] public TmdbTvWatchProviderTypeData[] Rent { get; set; } = [];
    [JsonProperty("free")] public TmdbTvWatchProviderTypeData[] Free { get; set; } = [];
}