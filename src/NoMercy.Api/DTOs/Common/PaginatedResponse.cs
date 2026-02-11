using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Common;

public record PaginatedResponse<T>
{
    [JsonProperty("data")] public IEnumerable<T> Data { get; set; } = [];
    [JsonProperty("next_page")] public int? NextPage { get; set; }
    [JsonProperty("has_more")] public bool HasMore { get; set; }
}