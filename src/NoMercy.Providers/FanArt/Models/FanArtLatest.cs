using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class FanArtLatest
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("new_images")] public string NewImages { get; set; } = string.Empty;
    [JsonProperty("total_images")] public string TotalImages { get; set; } = string.Empty;
}
