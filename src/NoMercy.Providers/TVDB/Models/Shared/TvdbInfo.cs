using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models.Shared;

public class TvdbInfo
{
    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("year")]
    public string? Year { get; set; }
}
