using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class KitsuIoMeta
{
    [JsonProperty("count")] public int Count { get; set; }
}