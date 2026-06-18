using NoMercy.Providers.NoMercy.Client;
using SixLabors.ImageSharp;

namespace NoMercy.MediaProcessing.Images;

public abstract class NoMercyImageManager : INoMercyImageManager
{
    private static readonly Size PaletteDecodeSize = new(
        ColorQuantizer.MaxDimension,
        ColorQuantizer.MaxDimension
    );

    public static async Task<string> ColorPalette(string type, string? path, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(
            NoMercyImageClient.Download,
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
            NoMercyImageClient.Download,
            items,
            download,
            PaletteDecodeSize
        );
    }
}
