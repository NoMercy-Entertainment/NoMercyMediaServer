using System.Collections.Concurrent;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.MediaProcessing.Jobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NoMercy.MediaProcessing.Images;

public class BaseImageManager (
    ImageRepository imageRepository,
    JobDispatcher jobDispatcher
): IBaseImageManager, IDisposable
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
        Image<Rgba32>? imageData = await client.Invoke(path, download);

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
            Image<Rgba32>? imageData = await client.Invoke(item.Path, download);
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
        GC.Collect();
        GC.WaitForFullGCComplete();
        GC.WaitForPendingFinalizers();
    }

    public static PaletteColors GetColorPaletteColors(Image<Rgba32> image)
    {
        const double luminanceThreshold = 128.0;

        Rgba32? lightMuted = null;
        Rgba32? darkMuted = null;

        Rgba32? lightVibrant = null;
        Rgba32? darkVibrant = null;

        ConcurrentDictionary<Rgba32, int> colorCounts = new();

        const double minLightVibrancy = 0.01;
        double maxLightVibrancy = 0.0;

        const double minDarkVibrancy = 0.01;
        double maxDarkVibrancy = 0.0;
        int length = image.Width * image.Height;

        Parallel.For(0, length, index =>
        {
            int x = index % image.Width;
            int y = index / image.Width;

            Rgba32 pixel = image[x, y];

            double luminance = 0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B;

            double saturation = GetSaturation(pixel);

            if (luminance > luminanceThreshold)
            {
                if (saturation > maxLightVibrancy)
                {
                    maxLightVibrancy = saturation;
                    lightVibrant = pixel;
                }
            }
            else
            {
                if (saturation > maxDarkVibrancy)
                {
                    maxDarkVibrancy = saturation;
                    darkVibrant = pixel;
                }
            }

            Rgba32 reducedColor = new(
                (byte)(pixel.R & 0xF0),
                (byte)(pixel.G & 0xF0),
                (byte)(pixel.B & 0xF0),
                pixel.A);

            if (!colorCounts.TryAdd(reducedColor, 1)) colorCounts[reducedColor]++;
        });

        Parallel.For(0, length, index =>
        {
            int x = index % image.Width;
            int y = index / image.Width;

            Rgba32 pixel = image[x, y];

            double luminance = 0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B;

            double saturation = GetSaturation(pixel);

            if (luminance > luminanceThreshold)
            {
                if (saturation > minLightVibrancy && saturation < maxLightVibrancy)
                {
                    maxLightVibrancy = saturation;
                    lightMuted = pixel;
                }
            }
            else
            {
                if (saturation > minDarkVibrancy && saturation < maxDarkVibrancy)
                {
                    maxDarkVibrancy = saturation;
                    darkMuted = pixel;
                }
            }
        });

        image.Mutate(x => x.Resize(new ResizeOptions()
        {
            Size = new(1, 1),
            Mode = ResizeMode.Max
        }));

        IEnumerable<KeyValuePair<Rgba32, int>> sortedColors = colorCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(1);

        return new()
        {
            Dominant = "#" + sortedColors.FirstOrDefault().Key.ToHex(),
            Primary = "#" + image[0, 0].ToHex(),
            LightVibrant = "#" + (lightVibrant ?? new Rgba32(255, 255, 255)).ToHex(),
            DarkVibrant = "#" + (darkVibrant ?? new Rgba32(0, 0, 0)).ToHex(),
            LightMuted = "#" + (lightMuted ?? new Rgba32(255, 255, 255)).ToHex(),
            DarkMuted = "#" + (darkMuted ?? new Rgba32(0, 0, 0)).ToHex()
        };
    }

    private static double GetSaturation(Rgba32 color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double saturation = max == 0 ? 0 : delta / max;

        return saturation;
    }

    private (double hue, double saturation, double value) RgbaToHsv(Rgba32 color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double hue = 0.0;
        double saturation = max == 0 ? 0 : delta / max;
        double value = max;

        if (delta == 0)
            hue = 0;
        else if (max == r)
            hue = (g - b) / delta + (g < b ? 6 : 0);
        else if (max == g)
            hue = (b - r) / delta + 2;
        else
            hue = (r - g) / delta + 4;

        return (hue, saturation, value);
    }
}
