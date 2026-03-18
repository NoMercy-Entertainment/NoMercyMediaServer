using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class MovieImages
{
    [JsonProperty("backdrops")] public TmdbImage[] Backdrops { get; set; } = [];
    [JsonProperty("posters")] public TmdbImage[] Posters { get; set; } = [];
    [JsonProperty("logos")] public TmdbImage[] Logos { get; set; } = [];
}