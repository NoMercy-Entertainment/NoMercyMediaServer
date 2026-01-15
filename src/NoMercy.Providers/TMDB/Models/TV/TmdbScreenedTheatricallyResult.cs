using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbScreenedTheatricallyResult
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("episode_number")] public int EpisodeNumber { get; set; }
    [JsonProperty("season_number")] public int SeasonNumber { get; set; }
}