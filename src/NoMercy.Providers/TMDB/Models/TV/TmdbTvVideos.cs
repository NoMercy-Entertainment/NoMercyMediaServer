using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvVideos
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public TmdbTvVideo[] Results { get; set; } = [];
}