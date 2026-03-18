using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCreatedBy
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("credit_id")] public string CreditId { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("gender")] public int Gender { get; set; }
    [JsonProperty("profile_path")] public string? ProfilePath { get; set; }
}