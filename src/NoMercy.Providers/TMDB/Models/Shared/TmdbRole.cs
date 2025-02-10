using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbRole
{
    [JsonProperty("credit_id")] public string CreditId { get; set; } = string.Empty;
    [JsonProperty("character")] public string Character { get; set; } = string.Empty;
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
}
