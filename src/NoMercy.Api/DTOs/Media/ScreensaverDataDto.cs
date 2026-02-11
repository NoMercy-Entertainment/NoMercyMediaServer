using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.DTOs.Media;

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