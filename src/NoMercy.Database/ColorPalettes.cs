using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        get => _colorPalette != string.Empty
            ? JsonConvert.DeserializeObject<IColorPalettes>(_colorPalette)
            : null;
        set => _colorPalette = JsonConvert.SerializeObject(value);
    }
}