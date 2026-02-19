using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Search;

public class TmdbKeywordSearchResult
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}