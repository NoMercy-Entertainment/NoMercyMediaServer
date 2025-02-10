using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;

public class TadbLanguageDescription
{
    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; } = string.Empty;
    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
}
