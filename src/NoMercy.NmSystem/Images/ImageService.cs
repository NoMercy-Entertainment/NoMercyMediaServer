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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Fully qualify SixLabors' Configuration: the sibling NoMercy.NmSystem.Configuration
// namespace shadows the unqualified name from inside NoMercy.NmSystem.Images.
using ImageSharpConfiguration = SixLabors.ImageSharp.Configuration;

namespace NoMercy.NmSystem.Images;

public class ImageService : IImageService
{
    public ImageService()
    {
        ImageSharpConfiguration.Default.ImageFormatsManager.AddImageFormat(AvifFormat.Instance);
        ImageSharpConfiguration.Default.ImageFormatsManager.SetEncoder(
            AvifFormat.Instance,
            new PngEncoder()
        );
    }

    public IImageFormat Parse(string format)
    {
        IImageFormat imageFormat;
        ImageSharpConfiguration.Default.ImageFormatsManager.TryFindFormatByFileExtension(
            "png",
            out imageFormat!
        );

        if (string.IsNullOrEmpty(format))
            return imageFormat;

        format = format.ToLowerInvariant();

        if (
            ImageSharpConfiguration.Default.ImageFormatsManager.TryFindFormatByFileExtension(
                format,
                out IImageFormat? imageFormat2
            )
        )
            return imageFormat2;

        return imageFormat;
    }

    public (byte[] data, string mimeType) ResizeMagickNet(
        string image,
        int? width,
        double? aspectRatio,
        string? type
    )
    {
        IImageFormat format = Parse(type ?? "png");

        if (!File.Exists(image))
            throw new("File not found");

        using Image<Rgba32> input = Image.Load<Rgba32>(image);

        double ratio = aspectRatio ?? input.Height / (float)input.Width;
        int targetWidth = width ?? input.Width;
        int targetHeight = (int)(targetWidth * ratio);

        input.Mutate(x => x.Resize(targetWidth, targetHeight));

        using MemoryStream memoryStream = new();
        input.Save(memoryStream, format);

        return (memoryStream.ToArray(), format.MimeTypes.First());
    }
}
