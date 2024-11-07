using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class FanArtLatest
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("new_images")] public string NewImages { get; set; }
    [JsonProperty("total_images")] public string TotalImages { get; set; }
}
