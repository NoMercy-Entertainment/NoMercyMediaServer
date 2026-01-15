using NoMercy.Providers.TMDB.Client;

namespace NoMercy.MediaProcessing.Images;

public class MovieDbImageManager : IMovieDbImageManager
{
    public static async Task<string> ColorPalette(string type, string? path, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(TmdbImageClient.Download, type, path, download);
    }

    public static async Task<string> MultiColorPalette(IEnumerable<BaseImageManager.MultiStringType> items,
        bool? download = true)
    {
        return await BaseImageManager.MultiColorPalette(TmdbImageClient.Download, items, download);
    }
}