using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.CoverArt.Models;
using NoMercy.Providers.Helpers;
using NoMercy.Setup;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Configuration = AcoustID.Configuration;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Providers.CoverArt.Client;

public class CoverArtCoverArtClient : CoverArtBaseClient
{
    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "CoverArtCoverArtClient has not been initialized. Call CoverArtCoverArtClient.Initialize() at startup."
        );

    public CoverArtCoverArtClient(Guid id)
        : base(id)
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
        string filePath = Path.Combine(
            AppFiles.MusicImagesPath,
            Path.GetFileName((url?.LocalPath).OrEmpty())
        );

        IStorage storage = Storage;
        if (await storage.ExistsAsync(filePath, CancellationToken.None))
            return Image.Load<Rgba32>(filePath);

        HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.CoverArtImage);

        using HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        if (download is not false && !await storage.ExistsAsync(filePath, CancellationToken.None))
            await storage.WriteAsync(filePath, bytes, CancellationToken.None);

        return Image.Load<Rgba32>(bytes);
    }
}
