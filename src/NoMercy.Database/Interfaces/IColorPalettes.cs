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

    /// <summary>
    /// Forgiving deserializer for the persisted color-palette JSON. Returns
    /// null for empty/missing/malformed strings so a corrupted DB row never
    /// takes a card render down. Use this everywhere instead of calling
    /// <see cref="JsonConvert.DeserializeObject{T}(string)"/> directly.
    /// </summary>
    public static IColorPalettes? FromJsonOrNull(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonConvert.DeserializeObject<IColorPalettes>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
