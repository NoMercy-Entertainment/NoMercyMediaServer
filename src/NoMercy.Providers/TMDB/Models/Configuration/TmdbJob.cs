using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Configuration;

public class TmdbJob
{
    [JsonProperty("department")] public string Department { get; set; } = string.Empty;
    [JsonProperty("jobs")] public string[] Jobs { get; set; } = [];
}