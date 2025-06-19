using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbTvReviews : TmdbPaginatedResponse<TmdbReviewsResult>
{
    [JsonProperty("id")] public int Id { get; set; }
}