using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Combined;

public class TmdbCombinedTranslationData
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("homepage")] public Uri? Homepage { get; set; }
    [JsonProperty("biography")] public string? Biography { get; set; }
    [JsonProperty("tagline")] public string? Tagline { get; set; }
}