using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionImages
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("backdrops")] public TmdbImage[] Backdrops { get; set; } = [];
    [JsonProperty("posters")] public TmdbImage[] Posters { get; set; } = [];
    [JsonProperty("logos")] public TmdbImage[] Logos { get; set; } = [];
}