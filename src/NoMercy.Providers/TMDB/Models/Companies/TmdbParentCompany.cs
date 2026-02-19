using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Companies;

public class TmdbParentCompany
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("logo_path")] public string? LogoPath { get; set; }
}