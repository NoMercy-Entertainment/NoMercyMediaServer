using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Credits;

public class TmdbCreditDetails
{
    [JsonProperty("credit_type")] public string CreditType { get; set; } = string.Empty;
    [JsonProperty("department")] public string Department { get; set; } = string.Empty;
    [JsonProperty("job")] public string Job { get; set; } = string.Empty;
    [JsonProperty("media")] public TmdbMedia? Media { get; set; }
    [JsonProperty("media_type")] public string? MediaType { get; set; }
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("person")] public TmdbPerson TmdbPerson { get; set; } = new();
}