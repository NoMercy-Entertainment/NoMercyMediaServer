using Newtonsoft.Json;
using NoMercy.Database;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public class ImageUploadResponseDto
{
    [JsonProperty("url")] public Uri Url { get; set; } = null!;
    
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
}