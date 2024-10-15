using NoMercy.Networking;
using NoMercy.NmSystem;
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

                string filePath = Path.Join(folder, path.Replace("/", ""));

                if (File.Exists(filePath)) return Image.Load<Rgba32>(filePath);

                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
                httpClient.BaseAddress = new Uri("https://image.nomercy.tv/");
                httpClient.DefaultRequestHeaders.Add("Accept", "image/*");
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                string url = path.StartsWith("http") ? path : $"original{path}";

                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                if (download is false)
                    return Image.Load<Rgba32>(await response.Content.ReadAsStreamAsync());

                if (!File.Exists(filePath))
                    await File.WriteAllBytesAsync(filePath, await response.Content.ReadAsByteArrayAsync());

                return Image.Load<Rgba32>(await response.Content.ReadAsStreamAsync());
            }
            catch (Exception e)
            {
                Logger.MovieDb($"Error downloading image: {path} - {e.Message}", LogEventLevel.Error);
            }

            return null;
        }
    }
}