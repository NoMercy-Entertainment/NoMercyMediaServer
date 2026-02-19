using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Combined;

public class TmdbCombinedTranslation
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("english_name")] public string EnglishName { get; set; } = string.Empty;
    [JsonProperty("data")] public TmdbCombinedTranslationData Data { get; set; } = new();
}