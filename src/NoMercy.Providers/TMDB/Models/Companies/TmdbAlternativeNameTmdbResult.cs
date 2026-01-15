using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Companies;

public class TmdbAlternativeNameTmdbResult
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}