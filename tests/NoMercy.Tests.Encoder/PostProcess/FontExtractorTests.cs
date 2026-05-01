using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.PostProcess;

public class FontExtractorTests : IDisposable
{
    private readonly FontExtractor _extractor = new(TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public FontExtractorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FontExtractorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand includes -dump_attachment:t flag
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_ContainsDumpAttachmentFlag()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir
        );

        cmd.Arguments.Should().Contain("-dump_attachment:t");
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand sets working directory to fonts subfolder
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_WorkingDirectoryIsFontsSubdir()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir
        );

        string expectedFontDir = Path.Combine(_tempDir, "fonts");
        cmd.WorkingDirectory.Should().Be(expectedFontDir);
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand uses configured ffmpeg path
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_UsesConfiguredFfmpegPath()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "/usr/bin/ffmpeg",
            "/input/movie.mkv",
            _tempDir
        );

        cmd.Executable.Should().Be("/usr/bin/ffmpeg");
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand references input path
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_ContainsInputPath()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir
        );

        cmd.Arguments.Should().Contain("/input/movie.mkv");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: font files produce correct JSON
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_WithFontFiles_WritesCorrectJson()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Font.ttf"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Another.otf"), "dummy");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string manifestPath = Path.Combine(_tempDir, "fonts.json");
        File.Exists(manifestPath).Should().BeTrue();

        string json = await File.ReadAllTextAsync(manifestPath);
        JArray entries = JArray.Parse(json);
        entries.Should().HaveCount(2);

        List<string> files = entries.Select(e => e["file"]!.Value<string>()!).ToList();
        files.Should().Contain("fonts/Font.ttf");
        files.Should().Contain("fonts/Another.otf");

        List<string> mimeTypes = entries.Select(e => e["mimeType"]!.Value<string>()!).ToList();
        mimeTypes.Should().Contain("application/x-font-truetype");
        mimeTypes.Should().Contain("application/x-font-opentype");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: WOFF and WOFF2 mime types
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_WoffFonts_CorrectMimeTypes()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Font.woff"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Font2.woff2"), "dummy");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "fonts.json"));
        JArray entries = JArray.Parse(json);

        List<string> mimeTypes = entries.Select(e => e["mimeType"]!.Value<string>()!).ToList();
        mimeTypes.Should().Contain("font/woff");
        mimeTypes.Should().Contain("font/woff2");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: empty fonts dir gets deleted, no manifest
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_EmptyFontsDir_DeletesDirAndNoManifest()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        Directory.Exists(fontDir).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: no fonts dir at all — no error, no manifest
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_NoFontsDir_DoesNotThrow()
    {
        // fonts/ subdirectory never created
        Func<Task> act = async () => await _extractor.WriteFontManifestAsync(_tempDir, default);

        await act.Should().NotThrowAsync();
        File.Exists(Path.Combine(_tempDir, "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Unknown extension gets octet-stream mime type
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_UnknownExtension_OctetStreamMimeType()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "font.bin"), "dummy");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "fonts.json"));
        JArray entries = JArray.Parse(json);
        entries[0]["mimeType"]!.Value<string>().Should().Be("application/octet-stream");
    }
}
