using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class PosterImage
{
    [JsonProperty("tiny")] public Uri? Tiny { get; set; }
    [JsonProperty("large")] public Uri? Large { get; set; }
    [JsonProperty("small")] public Uri? Small { get; set; }
    [JsonProperty("medium")] public Uri? Medium { get; set; }
    [JsonProperty("original")] public Uri? Original { get; set; }
    [JsonProperty("meta")] public CoverImageMeta? Meta { get; set; }
}