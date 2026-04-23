namespace NoMercy.Tests.Encoder.Subtitles;

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subtitles;

public class TesseractModelManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EncoderOptions _options;

    public TesseractModelManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TessTest_{Guid.NewGuid():N}");
        _options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            TesseractModelsDirectory = _tempDir,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Ensure_WhenModelExists_ReturnsPathWithoutNetwork()
    {
        Directory.CreateDirectory(_tempDir);
        string localPath = Path.Combine(_tempDir, "eng.traineddata");
        await File.WriteAllBytesAsync(localPath, [0xFF, 0xFE]);

        FakeHandler handler = new();
        TesseractModelManager manager = BuildManager(handler);

        string resolved = await manager.EnsureLanguageModelAsync("eng", CancellationToken.None);

        Assert.Equal(localPath, resolved);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Ensure_WhenModelMissing_DownloadsAndSaves()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04];
        FakeHandler handler = new() { ResponseBody = payload, StatusCode = HttpStatusCode.OK };
        TesseractModelManager manager = BuildManager(handler);

        string resolved = await manager.EnsureLanguageModelAsync("fra", CancellationToken.None);

        string expectedPath = Path.Combine(_tempDir, "fra.traineddata");
        Assert.Equal(expectedPath, resolved);
        Assert.Equal(1, handler.CallCount);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(payload, await File.ReadAllBytesAsync(expectedPath));
    }

    [Fact]
    public async Task Ensure_WhenRepositoryReturns404_Throws()
    {
        FakeHandler handler = new() { StatusCode = HttpStatusCode.NotFound };
        TesseractModelManager manager = BuildManager(handler);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.EnsureLanguageModelAsync("xyz", CancellationToken.None)
        );

        Assert.Contains("xyz.traineddata", ex.Message);
    }

    [Fact]
    public async Task Ensure_EmptyLanguage_ThrowsArgumentException()
    {
        TesseractModelManager manager = BuildManager(new());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.EnsureLanguageModelAsync("", CancellationToken.None)
        );
    }

    [Fact]
    public void GetDownloadedLanguages_WhenDirectoryMissing_ReturnsEmpty()
    {
        TesseractModelManager manager = BuildManager(new());

        IReadOnlyList<string> langs = manager.GetDownloadedLanguages();

        Assert.Empty(langs);
    }

    [Fact]
    public async Task GetDownloadedLanguages_ListsAllTrainedData()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "eng.traineddata"), "data");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "fra.traineddata"), "data");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "readme.txt"), "not a model");

        TesseractModelManager manager = BuildManager(new());

        IReadOnlyList<string> langs = manager.GetDownloadedLanguages();

        Assert.Contains("eng", langs);
        Assert.Contains("fra", langs);
        Assert.DoesNotContain("readme", langs);
    }

    [Fact]
    public async Task Ensure_WhenDownloadCancelled_DoesNotLeavePartialFile()
    {
        FakeHandler handler = new() { StatusCode = HttpStatusCode.OK, ResponseBody = [0x01] };
        CancellationTokenSource cts = new();
        TesseractModelManager manager = BuildManager(handler);
        handler.OnHandleRequest = () => cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.EnsureLanguageModelAsync("deu", cts.Token)
        );

        string expectedPath = Path.Combine(_tempDir, "deu.traineddata");
        Assert.False(File.Exists(expectedPath));
        Assert.False(File.Exists($"{expectedPath}.tmp"));
    }

    private TesseractModelManager BuildManager(FakeHandler handler)
    {
        HttpClient client = new(handler);
        return new(
            _options,
            client,
            NullLogger<TesseractModelManager>.Instance
        );
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public byte[] ResponseBody { get; set; } = [];
        public int CallCount { get; private set; }
        public Action? OnHandleRequest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            OnHandleRequest?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response = new(StatusCode);
            if (StatusCode == HttpStatusCode.OK)
            {
                response.Content = new ByteArrayContent(ResponseBody);
            }

            return Task.FromResult(response);
        }
    }
}
