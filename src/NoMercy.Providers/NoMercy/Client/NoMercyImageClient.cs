using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Storage;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.NoMercy.Client;

public abstract class NoMercyImageClient : TmdbBaseClient
{
    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "NoMercyImageClient has not been initialized. Call NoMercyImageClient.Initialize() at startup."
        );

    public static Task<Image<Rgba32>?> Download(
        string? path,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        return GetQueue().Enqueue(Task, $"original{path}", true);

        async Task<Image<Rgba32>?> Task()
        {
            if (path is null)
                return null;

            try
            {
                string folder = Path.Join(AppFiles.ImagesPath, "original");

                IStorage storage = Storage;
                await storage.CreateDirectoryAsync(folder, CancellationToken.None);

                string filePath = Path.Combine(folder, path.Replace("/", "").Replace("\\", ""));

                if (await storage.ExistsAsync(filePath, CancellationToken.None))
                {
                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return Image.Load<Rgba32>(options, filePath);
                    }

                    return Image.Load<Rgba32>(filePath);
                }

                HttpClient httpClient = HttpClientProvider.CreateClient(
                    HttpClientNames.NoMercyImage
                );

                string url = path.StartsWith("http") ? path : $"original{path}";

                using HttpResponseMessage response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                if (
                    download is not false
                    && !await storage.ExistsAsync(filePath, CancellationToken.None)
                )
                    await storage.WriteAsync(filePath, bytes, CancellationToken.None);

                if (maxDecodeSize.HasValue)
                {
                    DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                    return Image.Load<Rgba32>(options, bytes);
                }

                return Image.Load<Rgba32>(bytes);
            }
            catch (Exception e)
            {
                Logger.MovieDb(
                    $"Error downloading image: {path} - {e.Message}",
                    LogEventLevel.Error
                );
            }

            return null;
        }
    }
}
