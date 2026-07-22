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
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
// Fully qualify SixLabors' Configuration: the sibling NoMercy.NmSystem.Configuration
// namespace shadows the unqualified name from inside NoMercy.NmSystem.Images.
using ImageSharpConfiguration = SixLabors.ImageSharp.Configuration;

namespace NoMercy.NmSystem.Images;

public class ImageService : IImageService
{
    private const int DefaultAvifQuality = 75;

    public ImageService()
    {
        // ImageSharp cannot encode AVIF (HeyRed.ImageSharp.Heif is decode-only);
        // registering the format lets Parse resolve the "avif" extension and mime,
        // while ResizeMagickNet routes actual AVIF encoding through Magick.NET.
        ImageSharpConfiguration.Default.ImageFormatsManager.AddImageFormat(format: AvifFormat.Instance);
    }

    public IImageFormat Parse(string format)
    {
        IImageFormat imageFormat;
        ImageSharpConfiguration.Default.ImageFormatsManager.TryFindFormatByFileExtension(
            extension: "png",
            format: out imageFormat!
        );

        if (string.IsNullOrEmpty(value: format))
            return imageFormat;

        format = format.ToLowerInvariant();

        if (
            ImageSharpConfiguration.Default.ImageFormatsManager.TryFindFormatByFileExtension(
                extension: format,
                format: out IImageFormat? imageFormat2
            )
        )
            return imageFormat2;

        return imageFormat;
    }

    public (byte[] data, string mimeType) ResizeMagickNet(
        string image,
        int? width,
        double? aspectRatio,
        string? type,
        int? quality
    )
    {
        if (!File.Exists(path: image))
            throw new(message: "File not found");

        IImageFormat format = Parse(format: type ?? "png");

        return format is AvifFormat
            ? EncodeAvif(image: image, width: width, aspectRatio: aspectRatio, quality: quality ?? DefaultAvifQuality)
            : EncodeWithImageSharp(image: image, width: width, aspectRatio: aspectRatio, format: format);
    }

    private static (byte[] data, string mimeType) EncodeWithImageSharp(
        string image,
        int? width,
        double? aspectRatio,
        IImageFormat format
    )
    {
        using Image<Rgba32> input = Image.Load<Rgba32>(path: image);

        (int targetWidth, int targetHeight) = TargetSize(
            sourceWidth: input.Width,
            sourceHeight: input.Height,
            width: width,
            aspectRatio: aspectRatio
        );

        input.Mutate(operation: x => x.Resize(width: targetWidth, height: targetHeight));

        using MemoryStream memoryStream = new();
        input.Save(stream: memoryStream, format: format);

        return (memoryStream.ToArray(), format.MimeTypes.First());
    }

    private static (byte[] data, string mimeType) EncodeAvif(
        string image,
        int? width,
        double? aspectRatio,
        int quality
    )
    {
        using MagickImage magick = new(fileName: image);

        (int targetWidth, int targetHeight) = TargetSize(
            sourceWidth: (int)magick.Width,
            sourceHeight: (int)magick.Height,
            width: width,
            aspectRatio: aspectRatio
        );

        MagickGeometry geometry = new(width: (uint)targetWidth, height: (uint)targetHeight)
        {
            IgnoreAspectRatio = true,
        };
        magick.Resize(geometry: geometry);

        magick.Format = MagickFormat.Avif;
        magick.Quality = (uint)Math.Clamp(value: quality, min: 1, max: 100);

        return (magick.ToByteArray(), AvifFormat.Instance.DefaultMimeType);
    }

    private static (int width, int height) TargetSize(
        int sourceWidth,
        int sourceHeight,
        int? width,
        double? aspectRatio
    )
    {
        double ratio = aspectRatio ?? sourceHeight / (double)sourceWidth;
        int targetWidth = width ?? sourceWidth;
        int targetHeight = (int)(targetWidth * ratio);

        return (targetWidth, targetHeight);
    }
}
