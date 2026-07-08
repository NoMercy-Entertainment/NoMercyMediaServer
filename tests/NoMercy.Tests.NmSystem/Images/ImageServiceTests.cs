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

using System.Text;
using NoMercy.NmSystem.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.Tests.NmSystem.Images;

[Trait("Category", "Unit")]
public class ImageServiceTests : IDisposable
{
    private readonly ImageService _imageService = new();
    private readonly string _sourcePath = Path.Combine(
        Path.GetTempPath(),
        $"nomercy-image-test-{Guid.NewGuid():N}.png"
    );

    public ImageServiceTests()
    {
        // High-frequency detail so encoder quality measurably affects output size;
        // a flat colour compresses identically at every quality level.
        using Image<Rgba32> image = new(160, 160);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            byte red = (byte)((x * 37 + y * 17) % 256);
            byte green = (byte)((x * 11 + y * 53) % 256);
            byte blue = (byte)((x * 29 + y * 7) % 256);
            image[x, y] = new(red, green, blue, 255);
        }

        image.SaveAsPng(_sourcePath);
    }

    [Fact]
    public void ResizeMagickNet_EncodesRealAvif_NotAMislabeledPng()
    {
        (byte[] data, string mimeType) = _imageService.ResizeMagickNet(
            _sourcePath,
            60,
            null,
            "avif",
            50
        );

        mimeType.Should().Be("image/avif");
        IsAvif(data).Should().BeTrue("the bytes must be a real AVIF container, not PNG");
        IsPng(data).Should().BeFalse("AVIF must not silently fall back to PNG bytes");
    }

    [Fact]
    public void ResizeMagickNet_HonorsQuality_LowerQualityIsSmaller()
    {
        (byte[] high, _) = _imageService.ResizeMagickNet(_sourcePath, 120, null, "avif", 90);
        (byte[] low, _) = _imageService.ResizeMagickNet(_sourcePath, 120, null, "avif", 20);

        low.Length.Should().BeLessThan(high.Length);
    }

    [Fact]
    public void ResizeMagickNet_NonAvifType_StillUsesImageSharp()
    {
        (byte[] data, string mimeType) = _imageService.ResizeMagickNet(
            _sourcePath,
            60,
            null,
            "png",
            null
        );

        mimeType.Should().Be("image/png");
        IsPng(data).Should().BeTrue();
    }

    private static bool IsPng(byte[] data) =>
        data.Length >= 8
        && data[0] == 0x89
        && data[1] == 0x50
        && data[2] == 0x4E
        && data[3] == 0x47;

    private static bool IsAvif(byte[] data)
    {
        if (data.Length < 12)
            return false;

        string boxType = Encoding.ASCII.GetString(data, 4, 4);
        string majorBrand = Encoding.ASCII.GetString(data, 8, 4);

        return boxType == "ftyp" && majorBrand.Contains("avif");
    }

    public void Dispose()
    {
        if (File.Exists(_sourcePath))
            File.Delete(_sourcePath);

        GC.SuppressFinalize(this);
    }
}
