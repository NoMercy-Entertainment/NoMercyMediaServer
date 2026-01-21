using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record ScreensaverDataDto
{
    [JsonProperty("aspectRatio")] public double AspectRatio { get; set; }

    [JsonProperty("src")] public string? Src { get; set; }

    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    [JsonProperty("meta")] public MetaDto? Meta { get; set; }

    public ScreensaverDataDto(Image image, IEnumerable<Image> logos, string type)
    {
        Image? logo = logos.FirstOrDefault(x =>
            (type == Config.TvMediaType && x.TvId == image.TvId)
            || (type == Config.MovieMediaType && x.MovieId == image.MovieId));

        AspectRatio = image.AspectRatio;
        Src = image.FilePath;
        ColorPalette = image.ColorPalette;
        Meta = new()
        {
            Logo = logo is not null
                ? new LogoDto
                {
                    AspectRatio = logo.AspectRatio,
                    Src = logo.FilePath
                }
                : null
        };
    }
}