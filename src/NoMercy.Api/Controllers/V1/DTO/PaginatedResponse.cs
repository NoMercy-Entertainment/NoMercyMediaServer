using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.DTO;

public record PaginatedResponse<T>
{
    [JsonProperty("data")] public IEnumerable<T> Data { get; set; } = [];
    [JsonProperty("next_page")] public int? NextPage { get; set; }
    [JsonProperty("has_more")] public bool HasMore { get; set; }
}