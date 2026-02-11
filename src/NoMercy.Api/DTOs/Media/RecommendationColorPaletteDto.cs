using Newtonsoft.Json;
using NoMercy.Database;

namespace NoMercy.Api.DTOs.Media;

public record RecommendationColorPaletteDto
{
    [JsonProperty("poster")] public IColorPalettes? Poster { get; set; }
    [JsonProperty("backdrop")] public IColorPalettes? Backdrop { get; set; }
}