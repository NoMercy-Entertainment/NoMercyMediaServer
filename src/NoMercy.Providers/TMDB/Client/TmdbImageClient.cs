using NoMercy.Networking;
using NoMercy.NmSystem;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Providers.TMDB.Client;

public abstract class TmdbImageClient : TmdbBaseClient
{
    public static Task<Image<Rgba32>?>? Download(string? path, bool? download = true)
    {
        try
        {
            return GetQueue().Enqueue(Task, path, true);
        }
        catch (InvalidImageContentException e)
        {
            Logger.MovieDb($"Image format error downloading image: {path} - {e.Message}", LogEventLevel.Error);
            return null;
        }
        catch (ImageFormatException e)
        {
            Logger.MovieDb($"Image format error downloading image: {path} - {e.Message}", LogEventLevel.Error);
            return null;
        }

        async Task<Image<Rgba32>?> Task()
        {
            try
            {
                if (path is null) return null;

                bool isSvg = path.EndsWith(".svg");
                string folder = Path.Join(AppFiles.ImagesPath, "original");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string filePath = Path.Join(folder, path.Replace("/", ""));
                if (File.Exists(filePath))
                    return isSvg ? null : await Image.LoadAsync<Rgba32>(filePath);

                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                httpClient.BaseAddress = new("https://image.tmdb.org/t/p/");
                httpClient.DefaultRequestHeaders.Add("Accept", "image/*");
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                string url = path.StartsWith("http") ? path : $"original{path}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode) return null;

                if (download is false)
                    return isSvg ? null : Image.Load<Rgba32>(await response.Content.ReadAsStreamAsync());

                if (!File.Exists(filePath))
                    await File.WriteAllBytesAsync(filePath, await response.Content.ReadAsByteArrayAsync());

                try
                {
                    return isSvg ? null : Image.Load<Rgba32>(filePath);
                }
                catch (Exception e)
                {
                    Logger.MovieDb($"Error loading image: {path} - {e.Message}", LogEventLevel.Error);
                    return null;
                }
            }
            catch (InvalidImageContentException e)
            {
                Logger.MovieDb($"Image format error downloading image: {path} - {e.Message}", LogEventLevel.Error);
                return null;
            }
            catch (ImageFormatException e)
            {
                Logger.MovieDb($"Image format error downloading image: {path} - {e.Message}", LogEventLevel.Error);
                return null;
            }
        }
    }
}