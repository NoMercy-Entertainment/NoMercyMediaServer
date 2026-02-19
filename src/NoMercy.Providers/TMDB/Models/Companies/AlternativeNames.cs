using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Companies;

public class AlternativeNames
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbAlternativeNameTmdbResult[] Results { get; set; } = [];
}