using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class CoverImageMeta
{
    [JsonProperty("dimensions")] public Dimensions? Dimensions { get; set; }
}