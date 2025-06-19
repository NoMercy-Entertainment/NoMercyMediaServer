using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbPaymentType
{
    [JsonProperty("link")] public Uri? Link { get; set; }
    [JsonProperty("flatrate")] public TmdbPaymentDetails[] FlatRate { get; set; } = [];
    [JsonProperty("rent")] public TmdbPaymentDetails[] Rent { get; set; } = [];
    [JsonProperty("buy")] public TmdbPaymentDetails[] Buy { get; set; } = [];
}