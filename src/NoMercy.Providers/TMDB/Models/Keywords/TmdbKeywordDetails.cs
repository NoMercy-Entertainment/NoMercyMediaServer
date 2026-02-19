using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Keywords;

public class TmdbKeywordDetails
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}