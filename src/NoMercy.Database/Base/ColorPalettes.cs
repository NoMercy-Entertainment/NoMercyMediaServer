using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercy.Database;

public class ColorPalettes
{
    [Column("ColorPalette")]
    [StringLength(1024)]
    [JsonProperty("color_palette")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _colorPalette { get; set; } = string.Empty;

    [NotMapped]
    public IColorPalettes? ColorPalette
    {
        get => IColorPalettes.FromJsonOrNull(_colorPalette);
        set => _colorPalette = JsonConvert.SerializeObject(value);
    }
}
