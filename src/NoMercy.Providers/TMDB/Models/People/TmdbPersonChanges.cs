using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonChanges
{
    [JsonProperty("changes")] public TmdbPersonChange[] Changes { get; set; } = [];
}