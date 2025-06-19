using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbAggregatedCreditRole
{
    [JsonProperty("credit_id")] public string? CreditId { get; set; }
    [JsonProperty("character")] public string? Character { get; set; }
    [JsonProperty("episode_count")] public int EpisodeCount { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }
}