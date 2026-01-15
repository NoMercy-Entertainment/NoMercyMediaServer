using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbAggregatedCrewJob
{
    [JsonProperty("credit_id")] public string? CreditId { get; set; }
    [JsonProperty("job")] public string? Job { get; set; }
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }
}