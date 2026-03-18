using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonChange
{
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("items")] public TmdbSeasonChangeItem[] Items { get; set; } = [];
}