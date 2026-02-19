using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Episode;

public class TmdbEpisodeChanges
{
    [JsonProperty("changes")] public TmdbEpisodeChange[] Changes { get; set; } = [];
}