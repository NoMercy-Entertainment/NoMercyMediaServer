using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Sources;

public class TvdbSourceTypesResponse : TvdbResponse<TvdbSourceType[]> { }

public class TvdbSourceType
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("postfix")]
    public string? Postfix { get; set; }

    [JsonProperty("prefix")]
    public string? Prefix { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("sort")]
    public int Sort { get; set; }
}
