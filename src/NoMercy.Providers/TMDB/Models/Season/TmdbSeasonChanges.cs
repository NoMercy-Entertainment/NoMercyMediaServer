using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonChanges
{
    [JsonProperty("changes")] public TmdbSeasonChange[] Changes { get; set; } = [];
}