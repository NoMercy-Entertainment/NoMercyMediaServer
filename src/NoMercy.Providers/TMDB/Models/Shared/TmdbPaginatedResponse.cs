using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbPaginatedResponse<T>
{
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("results")] public List<T> Results { get; set; } = [];
    [JsonProperty("total_pages")] public int TotalPages { get; set; }
    [JsonProperty("total_results")] public int TotalResults { get; set; }
}