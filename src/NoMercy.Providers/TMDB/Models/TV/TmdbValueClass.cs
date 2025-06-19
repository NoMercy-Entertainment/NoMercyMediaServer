using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbValueClass
{
    [JsonProperty("season_id")] public int? SeasonId { get; set; }
    [JsonProperty("season_number")] public int? SeasonNumber { get; set; }
    [JsonProperty("id")] public int? Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("add_to_every_season")] public bool? AddToEverySeason { get; set; }
    [JsonProperty("character")] public string Character { get; set; } = string.Empty;
    [JsonProperty("credit_id")] public string CreditId { get; set; } = string.Empty;
    [JsonProperty("order")] public int? Order { get; set; }
    [JsonProperty("person_id")] public int? PersonId { get; set; }
    [JsonProperty("poster")] public TmdbPoster TmdbPoster { get; set; } = new();
    [JsonProperty("department")] public string Department { get; set; } = string.Empty;
    [JsonProperty("job")] public string Job { get; set; } = string.Empty;
}