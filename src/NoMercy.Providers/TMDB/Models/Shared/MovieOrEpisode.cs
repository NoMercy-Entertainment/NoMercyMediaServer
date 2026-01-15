using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public record MovieOrEpisode
{
    [JsonProperty("id")] public dynamic Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("duration")] public TimeSpan? Duration { get; set; }
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("episode_number")] public int EpisodeNumber { get; set; }
    [JsonProperty("season_number")] public int SeasonNumber { get; set; }
    [JsonProperty("still")] public string? Still { get; set; }
}