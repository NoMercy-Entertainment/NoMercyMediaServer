// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Database;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NoMercy.MediaProcessing.Images;

internal static class ColorQuantizer
{
    public const int MaxDimension = 128;
    private const int QuantizationBits = 5;
    private const int QuantizationShift = 8 - QuantizationBits;
    private const int QuantizationMask = (0xFF >> QuantizationShift) << QuantizationShift;
    private const int MaxSwatches = 32;

    private const double MinLuminance = 0.05;
    private const double MaxLuminance = 0.95;
    private const byte MinAlpha = 128;

    private const double SaturationWeight = 3.0;
    private const double LuminanceWeight = 6.5;
    private const double PopulationWeight = 0.5;

    public static PaletteColors ExtractPalette(Image<Rgba32> image)
    {
        Image<Rgba32> workingImage = DownsampleImage(image: image);
        List<QuantizedColor> pixels = ExtractAndFilterPixels(image: workingImage);

        if (pixels.Count == 0)
        {
            return EmptyPalette();
        }

        List<ColorSwatch> swatches = MedianCutQuantize(pixels: pixels);

        if (swatches.Count == 0)
        {
            return EmptyPalette();
        }

        return ScoreSwatches(swatches: swatches);
    }

    private static Image<Rgba32> DownsampleImage(Image<Rgba32> image)
    {
        if (image is { Width: <= MaxDimension, Height: <= MaxDimension })
        {
            return image.Clone();
        }

        double scale = Math.Min(
            val1: (double)MaxDimension / image.Width,
            val2: (double)MaxDimension / image.Height
        );
        int newWidth = Math.Max(val1: 1, val2: (int)(image.Width * scale));
        int newHeight = Math.Max(val1: 1, val2: (int)(image.Height * scale));

        Image<Rgba32> resized = image.Clone(operation: ctx => ctx.Resize(width: newWidth, height: newHeight));
        return resized;
    }

    private static List<QuantizedColor> ExtractAndFilterPixels(Image<Rgba32> image)
    {
        List<QuantizedColor> pixels = [];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Rgba32 pixel = image[x: x, y: y];

                if (pixel.A < MinAlpha)
                {
                    continue;
                }

                double luminance = GetLuminance(r: pixel.R, g: pixel.G, b: pixel.B);

                if (luminance < MinLuminance || luminance > MaxLuminance)
                {
                    continue;
                }

                byte qr = (byte)(pixel.R & QuantizationMask);
                byte qg = (byte)(pixel.G & QuantizationMask);
                byte qb = (byte)(pixel.B & QuantizationMask);

                pixels.Add(item: new(qr: qr, qg: qg, qb: qb, origR: pixel.R, origG: pixel.G, origB: pixel.B));
            }
        }

        image.Dispose();
        return pixels;
    }

    private static List<ColorSwatch> MedianCutQuantize(List<QuantizedColor> pixels)
    {
        List<ColorBox> boxes = [new(pixels: pixels)];

        while (boxes.Count < MaxSwatches)
        {
            ColorBox? largestBox = null;
            int largestIndex = -1;

            for (int i = 0; i < boxes.Count; i++)
            {
                if (
                    boxes[index: i].CanSplit && (largestBox is null || boxes[index: i].Volume > largestBox.Volume)
                )
                {
                    largestBox = boxes[index: i];
                    largestIndex = i;
                }
            }

            if (largestBox is null)
            {
                break;
            }

            (ColorBox left, ColorBox right) = largestBox.Split();
            boxes[index: largestIndex] = left;
            boxes.Add(item: right);
        }

        List<ColorSwatch> swatches = [];

        foreach (ColorBox box in boxes)
        {
            if (box.Population > 0)
            {
                swatches.Add(item: box.ToSwatch());
            }
        }

        return swatches;
    }

    private static PaletteColors ScoreSwatches(List<ColorSwatch> swatches)
    {
        int maxPopulation = 0;
        ColorSwatch dominantSwatch = swatches[index: 0];

        foreach (ColorSwatch swatch in swatches)
        {
            if (swatch.Population > maxPopulation)
            {
                maxPopulation = swatch.Population;
                dominantSwatch = swatch;
            }
        }

        SwatchTarget[] targets =
        [
            new(name: "LightVibrant", targetSaturation: 1.0, targetLuminance: 0.74),
            new(name: "Vibrant", targetSaturation: 1.0, targetLuminance: 0.50),
            new(name: "DarkVibrant", targetSaturation: 1.0, targetLuminance: 0.26),
            new(name: "LightMuted", targetSaturation: 0.3, targetLuminance: 0.74),
            new(name: "Muted", targetSaturation: 0.3, targetLuminance: 0.50),
            new(name: "DarkMuted", targetSaturation: 0.3, targetLuminance: 0.26),
        ];

        Dictionary<string, ColorSwatch?> assigned = new();
        HashSet<int> usedSwatchIndices = [];

        foreach (SwatchTarget target in targets)
        {
            double bestScore = double.MinValue;
            int bestIndex = -1;

            for (int i = 0; i < swatches.Count; i++)
            {
                if (usedSwatchIndices.Contains(item: i))
                {
                    continue;
                }

                ColorSwatch swatch = swatches[index: i];
                double satDistance = Math.Abs(value: swatch.Saturation - target.TargetSaturation);
                double lumDistance = Math.Abs(value: swatch.Luminance - target.TargetLuminance);
                double popNormalized =
                    maxPopulation > 0 ? (double)swatch.Population / maxPopulation : 0;

                double score =
                    1.0
                    - SaturationWeight * satDistance
                    - LuminanceWeight * lumDistance
                    - PopulationWeight * (1.0 - popNormalized);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                assigned[key: target.Name] = swatches[index: bestIndex];
                usedSwatchIndices.Add(item: bestIndex);
            }
            else
            {
                assigned[key: target.Name] = null;
            }
        }

        ColorSwatch? vibrant = assigned.GetValueOrDefault(key: "Vibrant");
        ColorSwatch? muted = assigned.GetValueOrDefault(key: "Muted");
        ColorSwatch primarySwatch = vibrant ?? muted ?? dominantSwatch;

        return new()
        {
            Dominant = SwatchToHex(swatch: dominantSwatch),
            Primary = SwatchToHex(swatch: primarySwatch),
            LightVibrant = SwatchToHex(
                swatch: assigned.GetValueOrDefault(key: "LightVibrant") ?? dominantSwatch
            ),
            DarkVibrant = SwatchToHex(swatch: assigned.GetValueOrDefault(key: "DarkVibrant") ?? dominantSwatch),
            LightMuted = SwatchToHex(swatch: assigned.GetValueOrDefault(key: "LightMuted") ?? dominantSwatch),
            DarkMuted = SwatchToHex(swatch: assigned.GetValueOrDefault(key: "DarkMuted") ?? dominantSwatch),
        };
    }

    private static string SwatchToHex(ColorSwatch swatch)
    {
        Rgba32 color = new(r: swatch.R, g: swatch.G, b: swatch.B);
        return "#" + color.ToHex();
    }

    private static PaletteColors EmptyPalette()
    {
        return new()
        {
            Dominant = "#808080FF",
            Primary = "#808080FF",
            LightVibrant = "#C0C0C0FF",
            DarkVibrant = "#404040FF",
            LightMuted = "#C0C0C0FF",
            DarkMuted = "#404040FF",
        };
    }

    private static double GetLuminance(byte r, byte g, byte b)
    {
        double rNorm = r / 255.0;
        double gNorm = g / 255.0;
        double bNorm = b / 255.0;

        double rLinear = rNorm <= 0.04045 ? rNorm / 12.92 : Math.Pow(x: (rNorm + 0.055) / 1.055, y: 2.4);
        double gLinear = gNorm <= 0.04045 ? gNorm / 12.92 : Math.Pow(x: (gNorm + 0.055) / 1.055, y: 2.4);
        double bLinear = bNorm <= 0.04045 ? bNorm / 12.92 : Math.Pow(x: (bNorm + 0.055) / 1.055, y: 2.4);

        return 0.2126 * rLinear + 0.7152 * gLinear + 0.0722 * bLinear;
    }

    private static (double saturation, double luminance) GetHsl(byte r, byte g, byte b)
    {
        double rNorm = r / 255.0;
        double gNorm = g / 255.0;
        double bNorm = b / 255.0;

        double max = Math.Max(val1: rNorm, val2: Math.Max(val1: gNorm, val2: bNorm));
        double min = Math.Min(val1: rNorm, val2: Math.Min(val1: gNorm, val2: bNorm));
        double delta = max - min;

        double luminance = (max + min) / 2.0;

        double saturation;

        if (delta == 0)
        {
            saturation = 0;
        }
        else
        {
            saturation = luminance <= 0.5 ? delta / (max + min) : delta / (2.0 - max - min);
        }

        return (saturation, luminance);
    }

    private readonly struct QuantizedColor(
        byte qr,
        byte qg,
        byte qb,
        byte origR,
        byte origG,
        byte origB
    )
    {
        public byte Qr { get; } = qr;
        public byte Qg { get; } = qg;
        public byte Qb { get; } = qb;
        public byte OrigR { get; } = origR;
        public byte OrigG { get; } = origG;
        public byte OrigB { get; } = origB;
    }

    private readonly struct ColorSwatch(
        byte r,
        byte g,
        byte b,
        int population,
        double saturation,
        double luminance
    )
    {
        public byte R { get; } = r;
        public byte G { get; } = g;
        public byte B { get; } = b;
        public int Population { get; } = population;
        public double Saturation { get; } = saturation;
        public double Luminance { get; } = luminance;
    }

    private readonly struct SwatchTarget(
        string name,
        double targetSaturation,
        double targetLuminance
    )
    {
        public string Name { get; } = name;
        public double TargetSaturation { get; } = targetSaturation;
        public double TargetLuminance { get; } = targetLuminance;
    }

    private sealed class ColorBox
    {
        private readonly List<QuantizedColor> _pixels;
        private readonly byte _minR,
            _maxR,
            _minG,
            _maxG,
            _minB,
            _maxB;

        public ColorBox(List<QuantizedColor> pixels)
        {
            _pixels = pixels;

            byte minR = 255,
                maxR = 0;
            byte minG = 255,
                maxG = 0;
            byte minB = 255,
                maxB = 0;

            foreach (QuantizedColor pixel in pixels)
            {
                if (pixel.Qr < minR)
                    minR = pixel.Qr;
                if (pixel.Qr > maxR)
                    maxR = pixel.Qr;
                if (pixel.Qg < minG)
                    minG = pixel.Qg;
                if (pixel.Qg > maxG)
                    maxG = pixel.Qg;
                if (pixel.Qb < minB)
                    minB = pixel.Qb;
                if (pixel.Qb > maxB)
                    maxB = pixel.Qb;
            }

            _minR = minR;
            _maxR = maxR;
            _minG = minG;
            _maxG = maxG;
            _minB = minB;
            _maxB = maxB;
        }

        public int Population => _pixels.Count;

        public int Volume
        {
            get
            {
                int rangeR = _maxR - _minR;
                int rangeG = _maxG - _minG;
                int rangeB = _maxB - _minB;
                return (rangeR + 1) * (rangeG + 1) * (rangeB + 1);
            }
        }

        public bool CanSplit => _pixels.Count >= 2;

        public (ColorBox left, ColorBox right) Split()
        {
            int rangeR = _maxR - _minR;
            int rangeG = _maxG - _minG;
            int rangeB = _maxB - _minB;

            Channel longestChannel;

            if (rangeR >= rangeG && rangeR >= rangeB)
            {
                longestChannel = Channel.R;
            }
            else if (rangeG >= rangeR && rangeG >= rangeB)
            {
                longestChannel = Channel.G;
            }
            else
            {
                longestChannel = Channel.B;
            }

            _pixels.Sort(
                comparison: (a, b) =>
                    longestChannel switch
                    {
                        Channel.R => a.Qr.CompareTo(value: b.Qr),
                        Channel.G => a.Qg.CompareTo(value: b.Qg),
                        Channel.B => a.Qb.CompareTo(value: b.Qb),
                        _ => 0,
                    }
            );

            int median = _pixels.Count / 2;

            List<QuantizedColor> leftPixels = _pixels.GetRange(index: 0, count: median);
            List<QuantizedColor> rightPixels = _pixels.GetRange(index: median, count: _pixels.Count - median);

            return (new(pixels: leftPixels), new(pixels: rightPixels));
        }

        public ColorSwatch ToSwatch()
        {
            long totalR = 0,
                totalG = 0,
                totalB = 0;

            foreach (QuantizedColor pixel in _pixels)
            {
                totalR += pixel.OrigR;
                totalG += pixel.OrigG;
                totalB += pixel.OrigB;
            }

            byte avgR = (byte)(totalR / _pixels.Count);
            byte avgG = (byte)(totalG / _pixels.Count);
            byte avgB = (byte)(totalB / _pixels.Count);

            (double saturation, double luminance) = GetHsl(r: avgR, g: avgG, b: avgB);

            return new(r: avgR, g: avgG, b: avgB, population: _pixels.Count, saturation: saturation, luminance: luminance);
        }

        private enum Channel
        {
            R,
            G,
            B,
        }
    }
}
