using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonChange
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("items")] public TmdbPersonChangeItem[] Items { get; set; } = [];
}