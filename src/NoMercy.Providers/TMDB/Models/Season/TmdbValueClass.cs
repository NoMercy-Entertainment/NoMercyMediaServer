using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbValueClass
{
    [JsonProperty("episode_id")] public int EpisodeId { get; set; }
    [JsonProperty("episode_number")] public int EpisodeNumber { get; set; }
}