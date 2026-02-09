using NoMercy.NmSystem.Information;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.Helpers;
using NoMercy.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Configuration = AcoustID.Configuration;

namespace NoMercy.Providers.CoverArt.Client;

public class CoverArtCoverArtClient : CoverArtBaseClient
{
    public CoverArtCoverArtClient(Guid id) : base(id)
    {
        Configuration.ClientKey = ApiInfo.AcousticIdKey;
    }

    public Task<CoverArtCovers?> Cover(bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        try
        {
            return Get<CoverArtCovers>("release/" + Id, queryParams, priority);
        }
        catch (Exception)
        {
            return Task.FromResult<CoverArtCovers?>(null);
        }
    }

    public Task<CoverArtCovers?> GroupCover(bool priority = false)
    {
        Dictionary<string, string> queryParams = new()
        {
            //
        };

        try
        {
            return Get<CoverArtCovers>("release-group/" + Id, queryParams, priority);
        }
        catch (Exception)
        {
            return Task.FromResult<CoverArtCovers?>(null);
        }
    }

    public static async Task<Image<Rgba32>?> Download(Uri? url, bool? download = true)
    {
        string filePath = Path.Combine(AppFiles.MusicImagesPath, Path.GetFileName(url?.LocalPath ?? string.Empty));

        if (File.Exists(filePath)) return Image.Load<Rgba32>(filePath);

        HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.CoverArtImage);

        HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        if (download is not false && !File.Exists(filePath))
            await File.WriteAllBytesAsync(filePath, bytes);

        return Image.Load<Rgba32>(bytes);
    }
}