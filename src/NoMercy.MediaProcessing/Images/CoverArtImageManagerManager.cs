using NoMercy.MediaProcessing.Jobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.CoverArt.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Images;

public class CoverArtImageManagerManager(
    ImageRepository imageRepository,
    JobDispatcher jobDispatcher
): ICoverArtImageManagerManager
{
    public static async Task<string> ColorPalette(string type, Uri url, bool? download = true)
    {
        return await BaseImageManager.ColorPalette(CoverArtCoverArtClient.Download, type, url, download);
    }

    public async Task<string> MultiColorPalette(IEnumerable<BaseImageManager.MultiUriType> items, bool? download = true)
    {
        return await BaseImageManager.MultiColorPalette(CoverArtCoverArtClient.Download, items, download);
    }
    
    public class CoverPalette
    {
        public string? Palette { get; set; }
        public Uri? Url { get; set; }
    }

    public static async Task<CoverPalette?> Add(Guid id)
    {
        try
        {
            CoverArtCoverArtClient coverArtCoverArtClient = new(id);
            CoverArtCovers? covers = await coverArtCoverArtClient.Cover();
            if (covers is null) return null;

            List<CoverArtImage> coverList = covers.Images
                .Where(image => image.Types.Contains("Front"))
                .ToList();

            foreach (CoverArtImage coverItem in coverList)
            {
                if (!coverItem.CoverArtThumbnails.Large.HasSuccessStatus("image/*")) continue;

                return new()
                {
                    Palette = await ColorPalette("cover", coverItem.CoverArtThumbnails.Large),
                    Url = coverItem.CoverArtThumbnails.Large
                };
            }

            return null;
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return null;
            Logger.FanArt(e.Message, LogEventLevel.Verbose);
            return null;
        }
    }
}