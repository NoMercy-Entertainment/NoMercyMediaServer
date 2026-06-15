using NoMercy.Providers.TMDB.Client;
using SixLabors.ImageSharp;

namespace NoMercy.MediaProcessing.Images;

public class MovieDbImageManager : IMovieDbImageManager
{
    private static readonly Size PaletteDecodeSize = new(
        ColorQuantizer.MaxDimension,
        ColorQuantizer.MaxDimension
    );

    public static async Task<string> ColorPalette(string type, string? path, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(
            TmdbImageClient.Download,
            type,
            path,
            download,
            PaletteDecodeSize
        );
    }

    public static async Task<string> MultiColorPalette(
        IEnumerable<BaseImageManager.MultiStringType> items,
        bool? download = true
    )
    {
        return await BaseImageManager.MultiColorPalette(
            TmdbImageClient.Download,
            items,
            download,
            PaletteDecodeSize
        );
    }
}
