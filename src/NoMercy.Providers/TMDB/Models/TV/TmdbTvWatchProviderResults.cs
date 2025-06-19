using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvWatchProviderResults
{
    [JsonProperty("AR")] public TmdbTvWatchProviderType Ar { get; set; } = new();
    [JsonProperty("AT")] public TmdbTvWatchProviderType At { get; set; } = new();
    [JsonProperty("AU")] public TmdbTvWatchProviderType Au { get; set; } = new();
    [JsonProperty("BE")] public TmdbTvWatchProviderType Be { get; set; } = new();
    [JsonProperty("BR")] public TmdbTvWatchProviderType Br { get; set; } = new();
    [JsonProperty("CA")] public TmdbTvWatchProviderType Ca { get; set; } = new();
    [JsonProperty("CH")] public TmdbTvWatchProviderType Ch { get; set; } = new();
    [JsonProperty("CL")] public TmdbTvWatchProviderType Cl { get; set; } = new();
    [JsonProperty("CO")] public TmdbTvWatchProviderType Co { get; set; } = new();
    [JsonProperty("CZ")] public TmdbTvWatchProviderType Cz { get; set; } = new();
    [JsonProperty("DE")] public TmdbTvWatchProviderType De { get; set; } = new();
    [JsonProperty("DK")] public TmdbTvWatchProviderType Dk { get; set; } = new();
    [JsonProperty("EC")] public TmdbTvWatchProviderType Ec { get; set; } = new();
    [JsonProperty("ES")] public TmdbTvWatchProviderType Es { get; set; } = new();
    [JsonProperty("FI")] public TmdbTvWatchProviderType Fi { get; set; } = new();
    [JsonProperty("FR")] public TmdbTvWatchProviderType Fr { get; set; } = new();
    [JsonProperty("GB")] public TmdbTvWatchProviderType Gb { get; set; } = new();
    [JsonProperty("HU")] public TmdbTvWatchProviderType Hu { get; set; } = new();
    [JsonProperty("IE")] public TmdbTvWatchProviderType Ie { get; set; } = new();
    [JsonProperty("IN")] public TmdbTvWatchProviderType In { get; set; } = new();
    [JsonProperty("IT")] public TmdbTvWatchProviderType It { get; set; } = new();
    [JsonProperty("JP")] public TmdbTvWatchProviderType Jp { get; set; } = new();
    [JsonProperty("MX")] public TmdbTvWatchProviderType Mx { get; set; } = new();
    [JsonProperty("NL")] public TmdbTvWatchProviderType Nl { get; set; } = new();
    [JsonProperty("NO")] public TmdbTvWatchProviderType No { get; set; } = new();
    [JsonProperty("NZ")] public TmdbTvWatchProviderType Nz { get; set; } = new();
    [JsonProperty("PE")] public TmdbTvWatchProviderType Pe { get; set; } = new();
    [JsonProperty("PL")] public TmdbTvWatchProviderType Pl { get; set; } = new();
    [JsonProperty("PT")] public TmdbTvWatchProviderType Pt { get; set; } = new();
    [JsonProperty("RO")] public TmdbTvWatchProviderType Ro { get; set; } = new();
    [JsonProperty("RU")] public TmdbTvWatchProviderType Ru { get; set; } = new();
    [JsonProperty("SE")] public TmdbTvWatchProviderType Se { get; set; } = new();
    [JsonProperty("TR")] public TmdbTvWatchProviderType Tr { get; set; } = new();
    [JsonProperty("US")] public TmdbTvWatchProviderType Us { get; set; } = new();
    [JsonProperty("VE")] public TmdbTvWatchProviderType Ve { get; set; } = new();
    [JsonProperty("ZA")] public TmdbTvWatchProviderType Za { get; set; } = new();
}