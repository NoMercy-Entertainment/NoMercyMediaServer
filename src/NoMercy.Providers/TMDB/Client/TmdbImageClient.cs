using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
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

                HttpClient httpClient = HttpClientProvider.CreateClient(HttpClientNames.TmdbImage);

                string url = path.StartsWith("http") ? path : $"original{path}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode) return null;

                if (download is false)
                {
                    await using Stream contentStream = await response.Content.ReadAsStreamAsync();
                    return isSvg ? null : Image.Load<Rgba32>(contentStream);
                }

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