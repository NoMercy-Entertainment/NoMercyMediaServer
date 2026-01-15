using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeChange
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("items")] public TmdbEpisodeChangeItem[] Items { get; set; } = [];
}