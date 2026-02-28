using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoMercy.Database;

public class ColorPaletteTimeStamps : Timestamps
{
    [Column("ColorPalette")]
    [StringLength(1024)]
    [JsonProperty("color_palette")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _colorPalette { get; set; }

    [NotMapped]
    public IColorPalettes? ColorPalette
    {
        get => !string.IsNullOrEmpty(_colorPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(_colorPalette)
            : null;
        set => _colorPalette = JsonConvert.SerializeObject(value);
    }
}