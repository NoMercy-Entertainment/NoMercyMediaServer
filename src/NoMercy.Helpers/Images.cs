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

using HeyRed.ImageSharp.Heif.Formats.Avif;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NoMercy.Helpers;

public static class Images
{
    // LOCAL-ONLY: NoMercy.Helpers cannot reference NoMercy.Providers (circular dependency).
    private static IStorageDriver _driver => new LocalStorageDriver();

    static Images()
    {
        Configuration.Default.ImageFormatsManager.AddImageFormat(AvifFormat.Instance);
        Configuration.Default.ImageFormatsManager.SetEncoder(AvifFormat.Instance, new PngEncoder());
    }

    private static byte[] ImageToByteArray(Image<Rgba32> image, ImageConvertArguments arguments)
    {
        using MemoryStream memoryStream = new();
        image.Save(memoryStream, arguments.Format);
        return memoryStream.ToArray();
    }

    public static (byte[] magickImage, string mimeType) ResizeMagickNet(
        string image,
        ImageConvertArguments arguments
    )
    {
        using Image<Rgba32> inputStream = ReadFileStream(image);
        double aspectRatio = arguments.AspectRatio ?? inputStream.Height / (float)inputStream.Width;

        int width = arguments.Width ?? inputStream.Width;
        int height = (int)(width * aspectRatio);

        inputStream.Mutate(x => x.Resize(width, height));
        return (ImageToByteArray(inputStream, arguments), arguments.Format.MimeTypes.First());
    }

    private static Image<Rgba32> ReadFileStream(string image)
    {
        if (!_driver.FileExists(image))
            throw new("File not found");

        // try
        // {
        return Image.Load<Rgba32>(image);
        // }
        // catch
        // {
        //     if (attempts < 5)
        //     {
        //         Thread.Sleep(100);
        //         return ReadFileStream(image, attempts + 1);
        //     }
        // }

        // throw new Exception("Failed to read image");
    }

    public static IImageFormat Parse(string format)
    {
        IImageFormat imageFormat;
        Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension(
            "png",
            out imageFormat!
        );

        if (string.IsNullOrEmpty(format))
            return imageFormat;

        format = format.ToLowerInvariant();

        if (
            Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension(
                format,
                out IImageFormat? imageFormat2
            )
        )
            return imageFormat2;

        return imageFormat;
    }
}
