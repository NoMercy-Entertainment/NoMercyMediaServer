using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models.Shared;

public class TvdbResponse<T>
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("data")]
    public T Data { get; set; } = default!;

    [JsonProperty("links")]
    public TvdbLinks? Links { get; set; }
}

public class TvdbPaginatedResponse<T>
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("data")]
    public List<T> Data { get; set; } = [];

    [JsonProperty("links")]
    public TvdbLinks? Links { get; set; }
}

public class TvdbLinks
{
    [JsonProperty("prev")]
    public string? Prev { get; set; }

    [JsonProperty("self")]
    public string? Self { get; set; }

    [JsonProperty("next")]
    public string? Next { get; set; }

    [JsonProperty("total_items")]
    public int TotalItems { get; set; }

    [JsonProperty("page_size")]
    public int PageSize { get; set; }
}
