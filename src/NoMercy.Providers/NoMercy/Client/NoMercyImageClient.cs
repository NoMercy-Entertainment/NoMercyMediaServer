using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.NoMercy.Client;

public abstract class NoMercyImageClient : TmdbBaseClient
{
    public static Task<Image<Rgba32>?> Download(string? path, bool? download = true)
    {
        return GetQueue().Enqueue(Task, $"original{path}", true);

        async Task<Image<Rgba32>?> Task()
        {
            if (path is null) return null;

            try
            {
                string folder = Path.Join(AppFiles.ImagesPath, "original");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, path.Replace("/", "").Replace("\\", ""));

                if (File.Exists(filePath)) return Image.Load<Rgba32>(filePath);

                HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.NoMercyImage);

                string url = path.StartsWith("http") ? path : $"original{path}";

                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

                if (download is not false && !File.Exists(filePath))
                    await File.WriteAllBytesAsync(filePath, bytes);

                return Image.Load<Rgba32>(bytes);
            }
            catch (Exception e)
            {
                Logger.MovieDb($"Error downloading image: {path} - {e.Message}", LogEventLevel.Error);
            }

            return null;
        }
    }
}