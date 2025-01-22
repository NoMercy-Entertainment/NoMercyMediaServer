using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record ScreensaverDataDto
{
    [JsonProperty("aspectRatio")] public double AspectRatio { get; set; }

    [JsonProperty("src")] public string? Src { get; set; }

    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    [JsonProperty("meta")] public Meta? Meta { get; set; }

    public ScreensaverDataDto(Image image, IEnumerable<Image> logos, string type)
    {
        Image? logo = logos.FirstOrDefault(x =>
            (type == "tv" && x.TvId == image.TvId)
            || (type == "movie" && x.MovieId == image.MovieId));

        AspectRatio = image.AspectRatio;
        Src = image.FilePath;
        ColorPalette = image.ColorPalette;
        Meta = new()
        {
            Logo = logo != null
                ? new Logo
                {
                    AspectRatio = logo.AspectRatio,
                    Src = logo.FilePath
                }
                : null
        };
    }
}
