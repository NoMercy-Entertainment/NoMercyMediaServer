using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Companies;

public class TmdbCompanyImages
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("logos")] public TmdbLogo[] Logos { get; set; } = [];
}