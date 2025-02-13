using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbResponse<T>
{
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("data")] public T Data { get; set; } = default!;
}

public class TvdbArtWorkStatusesResponse : TvdbResponse<TvdbStatus[]>
{
}
public class TvdbStatus
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string? Name { get; set; } = string.Empty;
}

public class TvdbTagOption
{
    [JsonProperty("helpText")] public string? HelpText { get; set; }
    [JsonProperty("id")] public int? Id { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("tag")] public int? Tag { get; set; }
    [JsonProperty("tagName")] public string? TagName { get; set; }
}