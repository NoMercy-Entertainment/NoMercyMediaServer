using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class FanArtLabel
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("musiclabel")] public MusicLabel[] Labels { get; set; } = [];
}
