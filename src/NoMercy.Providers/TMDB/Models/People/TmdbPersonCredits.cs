using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonCredits
{
    [JsonProperty("cast")] public TmdbPersonCredit[] Cast { get; set; } = [];
    [JsonProperty("crew")] public TmdbPersonCredit[] Crew { get; set; } = [];
}