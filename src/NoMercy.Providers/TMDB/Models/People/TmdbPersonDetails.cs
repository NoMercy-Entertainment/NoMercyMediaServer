using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonDetails : TmdbPerson
{
    [JsonProperty("deathday")] public DateTime? DeathDay { get; set; }
    [JsonProperty("homepage")] public Uri? Homepage { get; set; }
}