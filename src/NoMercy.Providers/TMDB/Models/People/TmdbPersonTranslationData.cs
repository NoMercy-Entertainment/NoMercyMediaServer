using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonTranslationData
{
    [JsonProperty("biography")] public string? Overview { get; set; }
}