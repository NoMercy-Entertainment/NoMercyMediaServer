using Newtonsoft.Json;

namespace NoMercy.Providers.CoverArt.Models;

public class CoverArtCovers
{
    [JsonProperty("images")] public CoverArtImage[] Images { get; set; } = [];
    [JsonProperty("release")] private string Release { get; set; } = string.Empty;
}
