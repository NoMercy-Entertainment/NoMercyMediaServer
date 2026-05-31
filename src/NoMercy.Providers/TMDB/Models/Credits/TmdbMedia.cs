using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Season;

namespace NoMercy.Providers.TMDB.Models.Credits;

public class TmdbMedia
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [JsonProperty("character")]
    public string Character { get; set; } = string.Empty;

    [JsonProperty("episodes")]
    public TmdbEpisode[] Episodes { get; set; } = [];

    [JsonProperty("seasons")]
    public TmdbSeason[] Seasons { get; set; } = [];
}
