using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonCombinedCredits
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("cast")] public TmdbPersonCredit[] Cast { get; set; } = [];
    [JsonProperty("crew")] public TmdbPersonCredit[] Crew { get; set; } = [];
}