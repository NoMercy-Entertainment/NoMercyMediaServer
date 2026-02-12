using Newtonsoft.Json;
using NoMercy.Database.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.DTOs.Media;


public class WatchProviderDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("provider_id")] public int WatchProviderId { get; set; }
    [JsonProperty("country_code")] public string CountryCode { get; set; } = string.Empty;
    [JsonProperty("type")] public string ProviderType { get; set; } = string.Empty; // "flatrate", "buy", "rent", "ads", "free"
    [JsonProperty("link")] public string? Link { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("logo")] public string? LogoPath { get; set; }
    [JsonProperty("display_priority")] public int DisplayPriority { get; set; }

    public WatchProviderDto()
    {
        
    }
    
    public WatchProviderDto(WatchProviderMedia wpm)
    {
        Id = wpm.Id;
        WatchProviderId = wpm.WatchProviderId;
        CountryCode = wpm.CountryCode;
        ProviderType = wpm.ProviderType;
        Link = wpm.Link;
        Name = wpm.WatchProvider.Name;
        LogoPath = wpm.WatchProvider.Logo;
        DisplayPriority = wpm.WatchProvider.DisplayPriority;
    }

    public WatchProviderDto((string CountryCode, string ProviderType, TmdbPaymentDetails Provider, string? Link) argKey)
    {
        CountryCode = argKey.CountryCode;
        ProviderType = argKey.ProviderType;
        WatchProviderId = argKey.Provider.ProviderId;
        Name = argKey.Provider.ProviderName;
        LogoPath = argKey.Provider.LogoPath;
        DisplayPriority = argKey.Provider.DisplayPriority;
        Link = argKey.Link;
    }
}