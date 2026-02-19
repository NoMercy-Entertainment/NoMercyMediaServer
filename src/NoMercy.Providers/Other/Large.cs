using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class Large
{
    [JsonProperty("width")] public int? Width { get; set; }
    [JsonProperty("height")] public int? Height { get; set; }
}