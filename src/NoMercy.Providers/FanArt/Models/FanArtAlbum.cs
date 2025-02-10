using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class FanArtAlbum
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("mbid_id")] public string MbId { get; set; } = string.Empty;
    [JsonProperty("albums")] public Dictionary<Guid, Albums> Albums { get; set; } = [];
}
