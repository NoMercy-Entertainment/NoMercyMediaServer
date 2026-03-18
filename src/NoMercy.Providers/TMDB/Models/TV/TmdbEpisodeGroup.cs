using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbEpisodeGroup
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("order")] public int Order { get; set; }
    [JsonProperty("episodes")] public TmdbEpisodeGroupEpisode[] Episodes { get; set; } = [];
    [JsonProperty("locked")] public bool Locked { get; set; }
}
