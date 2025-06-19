using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Credits;

public class TmdbMedia
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("original_name")] public string OriginalName { get; set; } = string.Empty;
    [JsonProperty("character")] public string Character { get; set; } = string.Empty;
    [JsonProperty("episodes")] public Episode.TmdbEpisode[] Episodes { get; set; } = [];
    [JsonProperty("seasons")] public Season.TmdbSeason[] Seasons { get; set; } = [];
}