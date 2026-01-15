using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbTmdbAggregatedCrew : TmdbAggregatedCredit
{
    [JsonProperty("jobs")] public TmdbAggregatedCrewJob[] Jobs { get; set; } = [];
}