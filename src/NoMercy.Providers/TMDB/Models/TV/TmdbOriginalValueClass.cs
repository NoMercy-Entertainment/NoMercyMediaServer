using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbOriginalValueClass
{
    [JsonProperty("id")] public int? Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("credit_id")] public string CreditId { get; set; } = string.Empty;
    [JsonProperty("person_id")] public int? PersonId { get; set; }
    [JsonProperty("season_id")] public int? SeasonId { get; set; }
    [JsonProperty("poster")] public TmdbPoster TmdbPoster { get; set; } = new();
    [JsonProperty("department")] public string Department { get; set; } = string.Empty;
    [JsonProperty("job")] public string Job { get; set; } = string.Empty;
}