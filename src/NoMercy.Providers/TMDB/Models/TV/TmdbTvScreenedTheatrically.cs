using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvScreenedTheatrically
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbScreenedTheatricallyResult[] Results { get; set; } = [];
}