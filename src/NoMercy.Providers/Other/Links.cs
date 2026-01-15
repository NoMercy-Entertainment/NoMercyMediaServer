using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class Links
{
    [JsonProperty("self")] public Uri Self { get; set; } = default!;
}