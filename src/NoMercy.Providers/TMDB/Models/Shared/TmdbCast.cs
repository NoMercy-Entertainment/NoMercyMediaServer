using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCast
{
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("gender")] public int Gender { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("known_for_department")] public string? KnownForDepartment { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("original_name")] public string? OriginalName { get; set; }
    [JsonProperty("popularity")] public float Popularity { get; set; }
    [JsonProperty("profile_path")] public string? ProfilePath { get; set; }
    [JsonProperty("character")] public string? Character { get; set; }
    [JsonProperty("credit_id")] public string? CreditId { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }
}