using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCertificationItem
{
    [JsonProperty("certification")] public string Certification { get; set; } = string.Empty;
    [JsonProperty("meaning")] public string Meaning { get; set; } = string.Empty;
    [JsonProperty("order")] public int Order { get; set; }
    public string Iso31661 { get; set; } = string.Empty;
}