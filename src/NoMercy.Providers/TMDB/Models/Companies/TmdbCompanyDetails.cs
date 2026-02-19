using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Companies;

public class TmdbCompanyDetails
{
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("headquarters")] public string? Headquarters { get; set; }
    [JsonProperty("homepage")] public Uri? Homepage { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("logo_path")] public string? LogoPath { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("origin_country")] public string? OriginCountry { get; set; }
    [JsonProperty("parent_company")] public TmdbParentCompany? ParentCompany { get; set; }
}