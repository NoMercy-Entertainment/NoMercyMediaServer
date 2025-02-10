using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbProductionCountry
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}
