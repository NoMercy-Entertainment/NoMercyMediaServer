using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbImages
{
    [JsonProperty("backdrops")] public TmdbImage[] Backdrops { get; set; } = [];
    [JsonProperty("posters")] public TmdbImage[] Posters { get; set; } = [];
    [JsonProperty("logos")] public TmdbImage[] Logos { get; set; } = [];
}