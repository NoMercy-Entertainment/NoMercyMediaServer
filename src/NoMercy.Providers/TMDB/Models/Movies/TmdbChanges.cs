using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbChanges
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("items")] public TmdbChange[] Items { get; set; } = [];
}