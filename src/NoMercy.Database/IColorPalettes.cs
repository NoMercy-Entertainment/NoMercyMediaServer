using Newtonsoft.Json;

namespace NoMercy.Database;

public class IColorPalettes
{
    [JsonProperty("poster", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Poster { get; set; }

    [JsonProperty("backdrop", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Backdrop { get; set; }

    [JsonProperty("still", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Still { get; set; }

    [JsonProperty("profile", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Profile { get; set; }

    [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Image { get; set; }

    [JsonProperty("cover", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Cover { get; set; }
}