using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class FanArtArtists
{
    [JsonProperty("cdart")] public Image[] CdArt { get; set; } = [];
    [JsonProperty("albumcover")] public Image[] AlbumCover { get; set; } = [];
}