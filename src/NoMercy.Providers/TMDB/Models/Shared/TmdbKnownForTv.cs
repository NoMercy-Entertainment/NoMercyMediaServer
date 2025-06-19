using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbKnownForTv
{
    [JsonProperty("first_air_date")] public DateTime? FirstAirDate { get; set; }
}