using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbInspirationTypesResponse: TvdbResponse<TvdbInspirationType[]>
{
}
public class TvdbInspirationType
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
    [JsonProperty("reference_name")] public string ReferenceName { get; set; } = string.Empty;
    [JsonProperty("url")] public Uri? Url { get; set; }
    
}