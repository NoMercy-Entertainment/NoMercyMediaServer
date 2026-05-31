using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models.Shared;

public class TvdbAlias
{
    [JsonProperty("language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
