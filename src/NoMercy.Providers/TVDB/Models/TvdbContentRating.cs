using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbContentRatingsResponse: TvdbResponse<TvdbContentRating[]>
{
}
    
public class TvdbContentRating
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
    [JsonProperty("country")] public string Country { get; set; } = string.Empty;
    [JsonProperty("contentType")] public string ContentType { get; set; } = string.Empty;
    [JsonProperty("order")] public int Order { get; set; }
    [JsonProperty("fullName")] public string FullName { get; set; } = string.Empty;
}