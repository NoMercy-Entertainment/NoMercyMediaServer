using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.WatchProviders;

public class TmdbAvailableRegionsResult
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("english_name")] public string EnglishName { get; set; } = string.Empty;
    [JsonProperty("native_name")] public string? NativeName { get; set; }
}