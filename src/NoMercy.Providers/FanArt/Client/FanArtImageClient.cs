using NoMercy.NmSystem.Information;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.Helpers;
using NoMercy.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Configuration = AcoustID.Configuration;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.FanArt.Client;

public class FanArtImageClient : FanArtBaseClient
{
    public FanArtImageClient()
    {
        Configuration.ClientKey = ApiInfo.AcousticIdKey;
    }
    
    public FanArtImageClient(Guid id) : base(id)
    {
        Configuration.ClientKey = ApiInfo.AcousticIdKey;
    }
    
    public Task<CoverArtCovers?> Cover(bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        return Get<CoverArtCovers>("release/" + Id, queryParams, priority);
    }

    public static async Task<Image<Rgba32>?> Download(Uri url, bool? download = true)
    {
        string filePath = Path.Combine(AppFiles.MusicImagesPath, Path.GetFileName(url.LocalPath));

        if (File.Exists(filePath)) return Image.Load<Rgba32>(filePath);

        HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.FanArtImage);

        using HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        if (download is not false && !File.Exists(filePath))
            await File.WriteAllBytesAsync(filePath, bytes);

        return Image.Load<Rgba32>(bytes);
    }
}