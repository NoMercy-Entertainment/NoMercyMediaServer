using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonImages
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("posters")] public TmdbImage[] Posters { get; set; } = [];
}