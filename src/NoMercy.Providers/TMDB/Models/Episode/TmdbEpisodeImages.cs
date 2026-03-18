using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeImages
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("stills")] public TmdbImage[] Stills { get; set; } = [];
}