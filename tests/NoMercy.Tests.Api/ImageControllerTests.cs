using System.Net;
using NoMercy.Helpers;
using NoMercy.NmSystem.Information;
using NoMercy.Tests.Api.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Characterization")]
public class ImageControllerTests : IClassFixture<NoMercyApiFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _testTypeFolder;
    private readonly string _testImageName = "testimage.png";
    private readonly string _testSvgName = "testimage.svg";

    public ImageControllerTests(NoMercyApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.AsAuthenticated();

        _testTypeFolder = Path.Join(AppFiles.ImagesPath, "testtype");
        if (!Directory.Exists(_testTypeFolder))
            Directory.CreateDirectory(_testTypeFolder);

        // Create a real 200x100 PNG test image
        using (Image<Rgba32> image = new(200, 100, new Rgba32(255, 0, 0)))
        {
            image.SaveAsPng(Path.Join(_testTypeFolder, _testImageName));
        }

        // Create a minimal SVG test file
        File.WriteAllText(Path.Join(_testTypeFolder, _testSvgName),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><rect fill=\"red\" width=\"100\" height=\"100\"/></svg>");

        // Ensure temp images directory exists
        if (!Directory.Exists(AppFiles.TempImagesPath))
            Directory.CreateDirectory(AppFiles.TempImagesPath);
    }

    public void Dispose()
    {
        // Clean up cached images created during tests
        string[] tempFiles = Directory.GetFiles(AppFiles.TempImagesPath);
        foreach (string file in tempFiles)
        {
            try { File.Delete(file); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Image_NoParams_ReturnsOriginalFile()
    {
        // No width, type, or quality params → emptyArguments = true → returns original
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string contentType = response.Content.Headers.ContentType!.MediaType!;
        Assert.Equal("image/png", contentType);

        byte[] originalBytes = await File.ReadAllBytesAsync(Path.Join(_testTypeFolder, _testImageName));
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(originalBytes.Length, responseBytes.Length);
    }

    [Fact]
    public async Task Image_WithWidth_ReturnsResizedImage()
    {
        // Width=50 → emptyArguments = false → image processing pipeline runs
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}?width=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        using Image<Rgba32> resultImage = Image.Load<Rgba32>(responseBytes);

        // Resized to width=50, aspect ratio preserved (200x100 → 50x25)
        Assert.Equal(50, resultImage.Width);
        Assert.Equal(25, resultImage.Height);
    }

    [Fact]
    public async Task Image_WithQualityNotDefault_ReturnsProcessedImage()
    {
        // Quality=80 → emptyArguments = false → processing runs
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}?quality=80");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        // The image was processed (not the raw original) — response should be valid image data
        Assert.True(responseBytes.Length > 0);
    }

    [Fact]
    public async Task Image_WithType_ReturnsProcessedImage()
    {
        // Type=png → emptyArguments = false (Type is not null) → processing runs
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}?type=png&width=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        using Image<Rgba32> resultImage = Image.Load<Rgba32>(responseBytes);
        Assert.Equal(100, resultImage.Width);
    }

    [Fact]
    public async Task Image_SvgBypassesProcessing()
    {
        // SVG files should bypass processing regardless of params
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testSvgName}?width=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] originalBytes = await File.ReadAllBytesAsync(Path.Join(_testTypeFolder, _testSvgName));
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(originalBytes.Length, responseBytes.Length);
    }

    [Fact]
    public async Task Image_ProcessedImageIsCached()
    {
        // First request: processes and caches
        HttpResponseMessage response1 = await _client.GetAsync($"/images/testtype/{_testImageName}?width=75");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        byte[] firstBytes = await response1.Content.ReadAsByteArrayAsync();

        // Second request: should serve from cache
        HttpResponseMessage response2 = await _client.GetAsync($"/images/testtype/{_testImageName}?width=75");
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        byte[] secondBytes = await response2.Content.ReadAsByteArrayAsync();

        Assert.Equal(firstBytes.Length, secondBytes.Length);
    }

    [Fact]
    public async Task Image_NonExistentType_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("/images/nonexistent/test.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Image_NonExistentFile_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("/images/testtype/doesnotexist.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Image_DefaultQuality100_NoWidth_NoType_ReturnsOriginal()
    {
        // Explicitly set quality=100 (the default) with no width/type → emptyArguments = true
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}?quality=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] originalBytes = await File.ReadAllBytesAsync(Path.Join(_testTypeFolder, _testImageName));
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(originalBytes.Length, responseBytes.Length);
    }

    [Fact]
    public async Task Image_CachingHeaders_AreSet()
    {
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Cache-Control"));
        Assert.Contains("public", response.Headers.GetValues("Cache-Control").First());
    }

    [Fact]
    public async Task Image_WithWidthAndAspectRatio_ReturnsCustomDimensions()
    {
        // Width=100 with aspect_ratio=2.0 → 100x200
        HttpResponseMessage response = await _client.GetAsync($"/images/testtype/{_testImageName}?width=100&aspect_ratio=2.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        using Image<Rgba32> resultImage = Image.Load<Rgba32>(responseBytes);
        Assert.Equal(100, resultImage.Width);
        Assert.Equal(200, resultImage.Height);
    }
}
