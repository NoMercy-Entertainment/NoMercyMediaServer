using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Configuration;

public class TmdbCountry
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("native_name")] public string NativeName { get; set; } = string.Empty;
    [JsonProperty("english_name")] public string EnglishName { get; set; } = string.Empty;
}