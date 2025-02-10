using Newtonsoft.Json;
using NoMercy.Database;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record RecommendationColorPaletteDto
{
    [JsonProperty("poster")] public IColorPalettes? Poster { get; set; }
    [JsonProperty("backdrop")] public IColorPalettes? Backdrop { get; set; }
}