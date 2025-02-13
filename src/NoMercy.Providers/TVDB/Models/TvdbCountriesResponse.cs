using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbCountriesResponse: TvdbResponse<TvdbCountry[]>
{
}
public class TvdbCountry
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("shortCode")] public string ShortCode { get; set; } = string.Empty;
}