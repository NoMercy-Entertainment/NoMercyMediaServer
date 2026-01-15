using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class KitsuAnime
{
    [JsonProperty("data")] public Data[] Data { get; set; } = [];
    [JsonProperty("meta")] public KitsuIoMeta Meta { get; set; } = new();
    [JsonProperty("links")] public KitsuIoLinks Links { get; set; } = new();
}