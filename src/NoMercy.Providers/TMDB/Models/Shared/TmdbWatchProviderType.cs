using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbWatchProviderType
{
    [JsonProperty("link")] public Uri? Link { get; set; }
    [JsonProperty("buy")] public TmdbPaymentDetails[] Buy { get; set; } = [];
    [JsonProperty("flatrate")] public TmdbPaymentDetails[] FlatRate { get; set; } = [];
    [JsonProperty("ads")] public TmdbPaymentDetails[] Ads { get; set; } = [];
    [JsonProperty("rent")] public TmdbPaymentDetails[] Rent { get; set; } = [];
    [JsonProperty("free")] public TmdbPaymentDetails[] Free { get; set; } = [];
}