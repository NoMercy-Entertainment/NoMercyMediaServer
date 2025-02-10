using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;
public class TmdbKeyword
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}