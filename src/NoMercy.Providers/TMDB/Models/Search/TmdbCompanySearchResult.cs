using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Search;

public class TmdbCompanySearchResult
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("logo_path")] public string? LogoPath { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}