using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Storage;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.TMDB.Client;

public abstract class TmdbImageClient : TmdbBaseClient
{
    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "TmdbImageClient has not been initialized. Call TmdbImageClient.Initialize() at startup."
        );

    public static Task<Image<Rgba32>?>? Download(
        string? path,
        bool? download = true,
        Size? maxDecodeSize = null
    )
    {
        try
        {
            return GetQueue().Enqueue(Task, path, true);
        }
        catch (InvalidImageContentException e)
        {
            Logger.MovieDb(
                $"Image format error downloading image: {path} - {e.Message}",
                LogEventLevel.Error
            );
            return null;
        }
        catch (ImageFormatException e)
        {
            Logger.MovieDb(
                $"Image format error downloading image: {path} - {e.Message}",
                LogEventLevel.Error
            );
            return null;
        }

        async Task<Image<Rgba32>?> Task()
        {
            try
            {
                if (path is null)
                    return null;

                bool isSvg = path.EndsWith(".svg");
                string folder = Path.Join(AppFiles.ImagesPath, "original");

                IStorage storage = Storage;
                await storage.CreateDirectoryAsync(folder, CancellationToken.None);

                string filePath = Path.Join(folder, path.Replace("/", ""));
                if (await storage.ExistsAsync(filePath, CancellationToken.None))
                {
                    if (isSvg)
                        return null;

                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return await Image.LoadAsync<Rgba32>(options, filePath);
                    }

                    return await Image.LoadAsync<Rgba32>(filePath);
                }

                HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.TmdbImage);

                string url = path.StartsWith("http") ? path : $"original{path}";
                using HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (download is false)
                {
                    if (isSvg)
                        return null;

                    await using Stream contentStream = await response.Content.ReadAsStreamAsync();

                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return Image.Load<Rgba32>(options, contentStream);
                    }

                    return Image.Load<Rgba32>(contentStream);
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                if (!await storage.ExistsAsync(filePath, CancellationToken.None))
                    await storage.WriteAsync(filePath, bytes, CancellationToken.None);

                try
                {
                    if (isSvg)
                        return null;

                    if (maxDecodeSize.HasValue)
                    {
                        DecoderOptions options = new() { TargetSize = maxDecodeSize.Value };
                        return Image.Load<Rgba32>(options, filePath);
                    }

                    return Image.Load<Rgba32>(filePath);
                }
                catch (Exception e)
                {
                    Logger.MovieDb(
                        $"Error loading image: {path} - {e.Message}",
                        LogEventLevel.Error
                    );
                    return null;
                }
            }
            catch (InvalidImageContentException e)
            {
                Logger.MovieDb(
                    $"Image format error downloading image: {path} - {e.Message}",
                    LogEventLevel.Error
                );
                return null;
            }
            catch (ImageFormatException e)
            {
                Logger.MovieDb(
                    $"Image format error downloading image: {path} - {e.Message}",
                    LogEventLevel.Error
                );
                return null;
            }
        }
    }
}
