using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.PostProcess;

public class ChapterWriterTests : IDisposable
{
    private readonly ChapterWriter _writer = new(TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public ChapterWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ChapterWriterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ------------------------------------------------------------------
    // Empty chapters list — no file created
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_EmptyChapters_NoFileCreated()
    {
        await _writer.WriteChaptersAsync(_tempDir, [], default);

        string chaptersFile = Path.Combine(_tempDir, "chapters.vtt");
        File.Exists(chaptersFile).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Valid chapters write a WEBVTT header
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_ValidChapters_WritesWebvttHeader()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(5), Title: "Opening"),
        ];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));
        content.Should().StartWith("WEBVTT");
    }

    // ------------------------------------------------------------------
    // Timestamps are formatted correctly
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_FormatsTimestampsCorrectly()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromSeconds(90), Title: "Part 1"),
            new(Start: TimeSpan.FromSeconds(90), End: TimeSpan.FromMinutes(30), Title: "Part 2"),
        ];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));

        content.Should().Contain("00:00:00.000 --> 00:01:30.000");
        content.Should().Contain("00:01:30.000 --> 00:30:00.000");
    }

    // ------------------------------------------------------------------
    // Chapter titles appear in the file
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_TitlesAppearInFile()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(5), Title: "Opening Credits"),
            new(
                Start: TimeSpan.FromMinutes(5),
                End: TimeSpan.FromMinutes(60),
                Title: "Main Feature"
            ),
        ];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));

        content.Should().Contain("Opening Credits");
        content.Should().Contain("Main Feature");
    }

    // ------------------------------------------------------------------
    // Null title falls back to "Chapter N"
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_NullTitle_FallsBackToChapterN()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(5), Title: null),
        ];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));
        content.Should().Contain("Chapter 1");
    }

    // ------------------------------------------------------------------
    // Multiple chapters all appear with correct cue numbers
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_MultipleChapters_CorrectCueNumbers()
    {
        ChapterInfo[] chapters =
        [
            new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "One"),
            new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), "Two"),
            new(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30), "Three"),
        ];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));

        content.Should().Contain("Chapter 1");
        content.Should().Contain("Chapter 2");
        content.Should().Contain("Chapter 3");
    }

    // ------------------------------------------------------------------
    // Hours component formats correctly for long durations
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_LongDuration_HoursFormattedCorrectly()
    {
        ChapterInfo[] chapters =
        [
            new(
                Start: TimeSpan.FromHours(1) + TimeSpan.FromMinutes(30),
                End: TimeSpan.FromHours(2),
                Title: "Act 3"
            ),
        ];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));
        content.Should().Contain("01:30:00.000 --> 02:00:00.000");
    }

    // ------------------------------------------------------------------
    // File is created at the expected path
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_CreatesFileAtCorrectPath()
    {
        ChapterInfo[] chapters = [new(TimeSpan.Zero, TimeSpan.FromMinutes(5), "Intro")];

        await _writer.WriteChaptersAsync(_tempDir, chapters, default);

        string expectedPath = Path.Combine(_tempDir, "chapters.vtt");
        File.Exists(expectedPath).Should().BeTrue();
    }
}
