using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class Dimensions
{
    [JsonProperty("tiny")] public Large? Tiny { get; set; }
    [JsonProperty("large?")] public Large? Large { get; set; }
    [JsonProperty("small")] public Large? Small { get; set; }

    [JsonProperty("medium")] public Large? Medium { get; set; }
}