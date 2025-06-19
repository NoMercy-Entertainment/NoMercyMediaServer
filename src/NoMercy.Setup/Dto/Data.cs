using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class Data
{
    [JsonProperty("state")] public string State { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("copyright")] public string Copyright { get; set; } = string.Empty;
    [JsonProperty("licence")] public string Licence { get; set; } = string.Empty;
    [JsonProperty("contact")] public Contact Contact { get; set; } = new();
    [JsonProperty("git")] public Uri? Git { get; set; }
    [JsonProperty("keys")] public Keys Keys { get; set; } = new();
    [JsonProperty("quote")] public string Quote { get; set; } = string.Empty;
    [JsonProperty("colors")] public string[] Colors { get; set; } = [];
    [JsonProperty("downloads")] public Downloads Downloads { get; set; } = new();
}