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

using System.Net;
using NoMercy.NmSystem.Information;
using NoMercy.Tests.Api.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Characterization")]
public class ImageControllerTests : IClassFixture<NoMercyApiFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _testTypeFolder;
    private readonly string _testId = Guid.NewGuid().ToString(format: "N")[..8];
    private readonly string _testImageName;
    private readonly string _testSvgName;

    public ImageControllerTests(NoMercyApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.AsAuthenticated();

        _testImageName = $"testimage_{_testId}.png";
        _testSvgName = $"testimage_{_testId}.svg";

        _testTypeFolder = Path.Join(path1: AppFiles.ImagesPath, path2: "testtype");
        if (!Directory.Exists(path: _testTypeFolder))
            Directory.CreateDirectory(path: _testTypeFolder);

        // Ensure temp images directory exists
        if (!Directory.Exists(path: AppFiles.TempImagesPath))
            Directory.CreateDirectory(path: AppFiles.TempImagesPath);

        // Create a real 200x100 PNG test image
        using (Image<Rgba32> image = new(width: 200, height: 100, backgroundColor: new(r: 255, g: 0, b: 0)))
        {
            image.SaveAsPng(path: Path.Join(path1: _testTypeFolder, path2: _testImageName));
        }

        // Create a minimal SVG test file
        File.WriteAllText(
            path: Path.Join(path1: _testTypeFolder, path2: _testSvgName),
            contents: "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><rect fill=\"red\" width=\"100\" height=\"100\"/></svg>"
        );
    }

    public void Dispose()
    {
        // Clean up test-specific files
        string imagePath = Path.Join(path1: _testTypeFolder, path2: _testImageName);
        string svgPath = Path.Join(path1: _testTypeFolder, path2: _testSvgName);
        try
        {
            if (File.Exists(path: imagePath))
                File.Delete(path: imagePath);
        }
        catch { }
        try
        {
            if (File.Exists(path: svgPath))
                File.Delete(path: svgPath);
        }
        catch { }

        // Clean up cached images created during tests
        foreach (string file in Directory.GetFiles(path: AppFiles.TempImagesPath))
        {
            try
            {
                File.Delete(path: file);
            }
            catch
            { /* best effort */
            }
        }
    }

    [Fact]
    public async Task Image_NoParams_ReturnsOriginalFile()
    {
        // No width, type, or quality params → emptyArguments = true → returns original
        HttpResponseMessage response = await _client.GetAsync(requestUri: $"/images/testtype/{_testImageName}");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        string contentType = response.Content.Headers.ContentType!.MediaType!;
        Assert.Equal(expected: "image/png", actual: contentType);

        byte[] originalBytes = await File.ReadAllBytesAsync(
            path: Path.Join(path1: _testTypeFolder, path2: _testImageName)
        );
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(expected: originalBytes.Length, actual: responseBytes.Length);
    }

    [Fact]
    public async Task Image_WithWidth_ReturnsResizedImage()
    {
        // Width=50 → emptyArguments = false → image processing pipeline runs
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?width=50"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        using Image<Rgba32> resultImage = Image.Load<Rgba32>(data: responseBytes);

        // Resized to width=50, aspect ratio preserved (200x100 → 50x25)
        Assert.Equal(expected: 50, actual: resultImage.Width);
        Assert.Equal(expected: 25, actual: resultImage.Height);
    }

    [Fact]
    public async Task Image_WithQualityNotDefault_ReturnsProcessedImage()
    {
        // Quality=80 → emptyArguments = false → processing runs
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?quality=80"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        // The image was processed (not the raw original) — response should be valid image data
        Assert.True(condition: responseBytes.Length > 0);
    }

    [Fact]
    public async Task Image_WithType_ReturnsProcessedImage()
    {
        // Type=png → emptyArguments = false (Type is not null) → processing runs
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?type=png&width=100"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        using Image<Rgba32> resultImage = Image.Load<Rgba32>(data: responseBytes);
        Assert.Equal(expected: 100, actual: resultImage.Width);
    }

    [Fact]
    public async Task Image_SvgBypassesProcessing()
    {
        // SVG files should bypass processing regardless of params
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testSvgName}?width=50"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        byte[] originalBytes = await File.ReadAllBytesAsync(
            path: Path.Join(path1: _testTypeFolder, path2: _testSvgName)
        );
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(expected: originalBytes.Length, actual: responseBytes.Length);
    }

    [Fact]
    public async Task Image_ProcessedImageIsCached()
    {
        // First request: processes and caches
        HttpResponseMessage response1 = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?width=75"
        );
        Assert.Equal(expected: HttpStatusCode.OK, actual: response1.StatusCode);
        byte[] firstBytes = await response1.Content.ReadAsByteArrayAsync();

        // Second request: should serve from cache
        HttpResponseMessage response2 = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?width=75"
        );
        Assert.Equal(expected: HttpStatusCode.OK, actual: response2.StatusCode);
        byte[] secondBytes = await response2.Content.ReadAsByteArrayAsync();

        Assert.Equal(expected: firstBytes.Length, actual: secondBytes.Length);
    }

    [Fact]
    public async Task Image_NonExistentType_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: "/images/nonexistent/test.png");

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task Image_NonExistentFile_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/doesnotexist_{_testId}.png"
        );

        Assert.Equal(expected: HttpStatusCode.NotFound, actual: response.StatusCode);
    }

    [Fact]
    public async Task Image_DefaultQuality100_NoWidth_NoType_ReturnsOriginal()
    {
        // Explicitly set quality=100 (the default) with no width/type → emptyArguments = true
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?quality=100"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        byte[] originalBytes = await File.ReadAllBytesAsync(
            path: Path.Join(path1: _testTypeFolder, path2: _testImageName)
        );
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(expected: originalBytes.Length, actual: responseBytes.Length);
    }

    [Fact]
    public async Task Image_CachingHeaders_AreSet()
    {
        HttpResponseMessage response = await _client.GetAsync(requestUri: $"/images/testtype/{_testImageName}");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.True(condition: response.Headers.Contains(name: "Cache-Control"));
        Assert.Contains(expectedSubstring: "public", actualString: response.Headers.GetValues(name: "Cache-Control").First());
    }

    [Fact]
    public async Task Image_WithWidthAndAspectRatio_ReturnsCustomDimensions()
    {
        // Width=100 with aspect_ratio=2.0 → 100x200
        HttpResponseMessage response = await _client.GetAsync(
            requestUri: $"/images/testtype/{_testImageName}?width=100&aspect_ratio=2.0"
        );

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        using Image<Rgba32> resultImage = Image.Load<Rgba32>(data: responseBytes);
        Assert.Equal(expected: 100, actual: resultImage.Width);
        Assert.Equal(expected: 200, actual: resultImage.Height);
    }
}
