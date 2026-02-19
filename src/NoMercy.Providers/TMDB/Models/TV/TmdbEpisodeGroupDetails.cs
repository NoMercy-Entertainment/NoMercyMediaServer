using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Networks;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbEpisodeGroupDetails
{
    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
    [JsonProperty("group_count")] public int GroupCount { get; set; }
    [JsonProperty("groups")] public TmdbEpisodeGroup[] Groups { get; set; } = [];
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("network")] public TmdbNetwork? Network { get; set; }
    [JsonProperty("type")] public int Type { get; set; }
}
