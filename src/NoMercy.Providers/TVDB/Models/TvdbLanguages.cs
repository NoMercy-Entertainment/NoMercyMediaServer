using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbLanguagesResponse: TvdbResponse<TvdbLanguage[]>
{
}
public class TvdbLanguage
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("nativeName")] public string NativeName { get; set; } = string.Empty;
    [JsonProperty("shortCode")] public string ShortCode { get; set; } = string.Empty;
}