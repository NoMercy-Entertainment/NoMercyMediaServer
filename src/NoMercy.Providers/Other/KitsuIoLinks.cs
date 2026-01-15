using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class KitsuIoLinks
{
    [JsonProperty("first")] public Uri First { get; set; } = default!;
    [JsonProperty("last")] public Uri Last { get; set; } = default!;
}