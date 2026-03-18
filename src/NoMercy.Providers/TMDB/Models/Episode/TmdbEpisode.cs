using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisode
{
    [JsonProperty("air_date")] public DateTime? AirDate { get; set; }
    [JsonProperty("episode_number")] public int EpisodeNumber { get; set; }
    [JsonProperty("name")] public string? Name { get; set; } = string.Empty;
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("production_code")] public string? ProductionCode { get; set; }
    [JsonProperty("season_number")] public int SeasonNumber { get; set; }
    [JsonProperty("still_path")] public string? StillPath { get; set; }
    [JsonProperty("vote_average")] public float? VoteAverage { get; set; }
    [JsonProperty("vote_count")] public int VoteCount { get; set; }
}