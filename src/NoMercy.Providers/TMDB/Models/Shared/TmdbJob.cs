using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbJob
{
    [JsonProperty("credit_id")] public string CreditId { get; set; } = string.Empty;
    [JsonProperty("job")] public string JobJob { get; set; } = string.Empty;
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
}
