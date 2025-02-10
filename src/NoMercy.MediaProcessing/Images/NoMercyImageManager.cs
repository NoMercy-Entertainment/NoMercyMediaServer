using NoMercy.Providers.NoMercy.Client;

namespace NoMercy.MediaProcessing.Images;

public abstract class NoMercyImageManager() : INoMercyImageManager
{
    public static async Task<string> ColorPalette(string type, string? path, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(NoMercyImageClient.Download, type, path, download);
    }

    public static async Task<string> MultiColorPalette(IEnumerable<BaseImageManager.MultiStringType> items, bool? download = true)
    {
        return await BaseImageManager.MultiColorPalette(NoMercyImageClient.Download, items, download);
    }
}