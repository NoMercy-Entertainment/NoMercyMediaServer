using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbWatchProviders
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbWatchProviderResults TmdbWatchProviderResults { get; set; } = new();

    public static IEnumerable<(string CountryCode, string ProviderType, TmdbPaymentDetails Provider, string? Link)> ExtractProviders(TmdbWatchProviderResults results)
    {
        foreach ((string key, TmdbWatchProviderType value) in results)
        {
            string countryCode = key.ToUpper();
            string? link = value.Link?.ToString();

            Dictionary<string, TmdbPaymentDetails[]?> providerTypeMap = new()
            {
                ["flatrate"] = value.FlatRate,
                ["buy"] = value.Buy,
                ["rent"] = value.Rent,
                ["ads"] = value.Ads,
                ["free"] = value.Free
            };

            foreach ((string providerType, TmdbPaymentDetails[]? providers) in providerTypeMap)
            {
                if (providers == null) continue;
            
                foreach (TmdbPaymentDetails provider in providers)
                {
                    yield return (countryCode, providerType, provider, link);
                }
            }
        }
        
    }
}