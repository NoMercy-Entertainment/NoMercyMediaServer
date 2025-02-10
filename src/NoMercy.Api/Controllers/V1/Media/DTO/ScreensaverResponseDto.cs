using Newtonsoft.Json;
using NoMercy.Database;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ScreensaverResponseDto
{
    [JsonProperty("aspectRatio")] public double AspectRatio { get; set; }
    [JsonProperty("src")] public string Src { get; set; } = string.Empty;
    [JsonProperty("color_palette")] public IColorPalettes? ColorPaletteDto { get; set; }
    [JsonProperty("meta")] public MetaDto MetaDto { get; set; } = new();
}
