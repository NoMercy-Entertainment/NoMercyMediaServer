using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvContentRatings
{
    [JsonProperty("results")] public TmdbTvContentRating[] Results { get; set; } = [];
}
