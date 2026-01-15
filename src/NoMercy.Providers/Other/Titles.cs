using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class Titles
{
    [JsonProperty("en")] public string? En { get; set; }
    [JsonProperty("en_jp")] public string? EnJp { get; set; }
    [JsonProperty("ja_jp")] public string? JaJp { get; set; }
    [JsonProperty("th_th")] public string? ThTh { get; set; }
}