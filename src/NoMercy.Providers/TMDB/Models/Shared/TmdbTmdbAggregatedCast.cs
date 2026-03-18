using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbTmdbAggregatedCast : TmdbAggregatedCredit
{
    [JsonProperty("roles")] public TmdbAggregatedCreditRole[] Roles { get; set; } = [];
}