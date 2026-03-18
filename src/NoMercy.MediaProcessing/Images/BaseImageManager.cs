using Newtonsoft.Json;
using NoMercy.Database;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Images;

public class BaseImageManager : IBaseImageManager, IDisposable
{
    public delegate Task<Image<Rgba32>?> DownloadUrl(Uri path, bool? download);

    public delegate Task<Image<Rgba32>?>? DownloadPath(string? path, bool? download);

    public class ColorPaletteArgument
    {
        public required string Key { get; set; }
        public Image<Rgba32>? ImageData { get; set; }
    }

    public class MultiUriType(string key, Uri url)
    {
        public readonly string Key = key;
        public readonly Uri Url = url;
    }

    public class MultiStringType(string key, string? path)
    {
        public readonly string Key = key;
        public readonly string? Path = path;
    }

    public static string GenerateColorPalette(IEnumerable<ColorPaletteArgument> items)
    {
        Dictionary<string, PaletteColors?> palette = new();

        foreach (ColorPaletteArgument item in items) palette.Add(item.Key, ColorPaletteFromImage(item.ImageData));

        IEnumerable<KeyValuePair<string, PaletteColors?>> palettes = palette
            .Where(x => x.Value != null);

        Dictionary<string, PaletteColors?> dict = palettes
            .ToDictionary(x => x.Key, x => x.Value);

        return JsonConvert.SerializeObject(dict);
    }

    public static PaletteColors ColorPaletteFromImage(Image<Rgba32>? image)
    {
        if (image is null)
            return new()
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                Dominant = null,
                Primary = null,
                LightVibrant = null,
                DarkVibrant = null,
                LightMuted = null,
                DarkMuted = null
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            };

        return GetColorPaletteColors(image);
    }

    public static async Task<string> ColorPalette(DownloadUrl client, string type, Uri path, bool? download = true)
    {
        Image<Rgba32>? imageData = await client.Invoke(path, download);
        if (imageData == null)
            return "";

        return GenerateColorPalette(new List<ColorPaletteArgument>
        {
            new()
            {
                Key = type,
                ImageData = imageData
            }
        });
    }

    public static async Task<string> MultiColorPalette(DownloadUrl client, IEnumerable<MultiUriType> items,
        bool? download = true)
    {
        List<ColorPaletteArgument> list = new();
        foreach (MultiUriType item in items)
        {
            Image<Rgba32>? imageData = await client.Invoke(item.Url, download);
            list.Add(new()
            {
                Key = item.Key,
                ImageData = imageData
            });
        }

        return GenerateColorPalette(list);
    }

    public static async Task<string> ColorPalette(DownloadPath client, string type, string? path,
        bool? download = true)
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        Image<Rgba32>? imageData = await client.Invoke(path, download);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        return GenerateColorPalette(new List<ColorPaletteArgument>
        {
            new()
            {
                Key = type,
                ImageData = imageData
            }
        });
    }

    public static async Task<string> MultiColorPalette(DownloadPath client, IEnumerable<MultiStringType> items,
        bool? download = true)
    {
        List<ColorPaletteArgument> list = new();
        foreach (MultiStringType item in items)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            Image<Rgba32>? imageData = await client.Invoke(item.Path, download);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            list.Add(new()
            {
                Key = item.Key,
                ImageData = imageData
            });
        }

        return GenerateColorPalette(list);
    }

    public void Dispose()
    {
    }

    public static PaletteColors GetColorPaletteColors(Image<Rgba32> image)
    {
        return ColorQuantizer.ExtractPalette(image);
    }
}