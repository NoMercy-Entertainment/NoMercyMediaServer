using NoMercy.Providers.NoMercy.Client;

namespace NoMercy.MediaProcessing.Images;

public abstract class NoMercyImage : BaseImage
{
    public static async Task<string> ColorPalette(string type, string? path, bool? download = true)
    {
        return await BaseImage.ColorPalette(NoMercyImageClient.Download, type, path, download);
    }

    public static async Task<string> MultiColorPalette(IEnumerable<MultiStringType> items, bool? download = true)
    {
        return await BaseImage.MultiColorPalette(NoMercyImageClient.Download, items, download);
    }
}