using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzPaginatedResponse<T>
{
    [JsonProperty("page")] public int Page { get; set; }
    [JsonProperty("results")] public T[] Results { get; set; } = [];
    [JsonProperty("total_pages")] public int TotalPages { get; set; }
    [JsonProperty("total_results")] public int TotalResults { get; set; }
}