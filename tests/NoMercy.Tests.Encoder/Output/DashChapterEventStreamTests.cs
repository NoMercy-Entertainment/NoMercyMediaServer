namespace NoMercy.Tests.Encoder.Output;

using System.Text;
using System.Xml.Linq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

/// <summary>
/// Verifies that <see cref="DashOutputStrategy.FinalizeAsync"/> injects an
/// <c>&lt;EventStream&gt;</c> with one <c>&lt;Event&gt;</c> per chapter into
/// every <c>&lt;Period&gt;</c> of the MPD.
/// </summary>
public class DashChapterEventStreamTests : IDisposable
{
    private readonly string _tempDir;

    public DashChapterEventStreamTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DashChapterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // Minimal MPD with one Period so we can verify post-processing
    private const string MinimalMpd = """
        <?xml version="1.0" encoding="utf-8"?>
        <MPD xmlns="urn:mpeg:dash:schema:mpd:2011" minBufferTime="PT1.5S" type="static" mediaPresentationDuration="PT5400S" profiles="urn:mpeg:dash:profile:isoff-on-demand:2011">
          <Period id="0" start="PT0S" duration="PT5400S">
            <AdaptationSet contentType="video" />
          </Period>
        </MPD>
        """;

    private static readonly IReadOnlyList<ChapterInfo> ThreeChapters =
    [
        new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Opening"),
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), "Act One & Two"),
        new(TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(90), "<Finale>"),
    ];

    // ------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAsync_WithChapters_InjectsEventStreamElement()
    {
        string mpdPath = await WriteMpdAndFinalize("movie1");

        string xml = await File.ReadAllTextAsync(mpdPath);
        xml.Should().Contain("EventStream");
        xml.Should().Contain("urn:nomercy:chapters");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_TimescaleIs1000()
    {
        string mpdPath = await WriteMpdAndFinalize("movie2");

        string xml = await File.ReadAllTextAsync(mpdPath);
        xml.Should().Contain("timescale=\"1000\"");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_EmitsOneEventPerChapter()
    {
        string mpdPath = await WriteMpdAndFinalize("movie3");

        XDocument doc = XDocument.Load(mpdPath);
        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        List<XElement> events = doc.Descendants(ns + "Event").ToList();

        events.Should().HaveCount(ThreeChapters.Count);
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_EventPresentationTimesAreInMilliseconds()
    {
        string mpdPath = await WriteMpdAndFinalize("movie4");

        XDocument doc = XDocument.Load(mpdPath);
        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        List<XElement> events = doc.Descendants(ns + "Event").ToList();

        // Chapter 1: 0 ms, Chapter 2: 10 min = 600000 ms, Chapter 3: 50 min = 3000000 ms
        events[0].Attribute("presentationTime")!.Value.Should().Be("0");
        events[1].Attribute("presentationTime")!.Value.Should().Be("600000");
        events[2].Attribute("presentationTime")!.Value.Should().Be("3000000");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_EventIdsAreZeroBasedIndex()
    {
        string mpdPath = await WriteMpdAndFinalize("movie5");

        XDocument doc = XDocument.Load(mpdPath);
        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        List<XElement> events = doc.Descendants(ns + "Event").ToList();

        events[0].Attribute("id")!.Value.Should().Be("0");
        events[1].Attribute("id")!.Value.Should().Be("1");
        events[2].Attribute("id")!.Value.Should().Be("2");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_TitlesAreXmlEscaped()
    {
        string mpdPath = await WriteMpdAndFinalize("movie6");

        // Raw XML text — the & and < in titles must be entity-escaped
        string rawXml = await File.ReadAllTextAsync(mpdPath);
        rawXml.Should().Contain("Act One &amp; Two");
        rawXml.Should().Contain("&lt;Finale&gt;");
    }

    [Fact]
    public async Task FinalizeAsync_WithoutChapters_DoesNotAddEventStream()
    {
        string mpdPath = WriteMpd("movie7");
        OutputPlan plan = CreatePlan(chapters: null);

        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        await strategy.FinalizeAsync(_tempDir, plan, "movie7", default);

        string xml = await File.ReadAllTextAsync(mpdPath);
        xml.Should().NotContain("EventStream");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Writes manifest.mpd, runs FinalizeAsync, returns path to renamed MPD.</summary>
    private async Task<string> WriteMpdAndFinalize(string title)
    {
        WriteMpd(title);
        OutputPlan plan = CreatePlan(ThreeChapters);
        DashOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        await strategy.FinalizeAsync(_tempDir, plan, title, default);
        return Path.Combine(_tempDir, $"{title}.mpd");
    }

    private string WriteMpd(string title)
    {
        // FinalizeAsync renames manifest.mpd → {title}.mpd so we always write manifest.mpd
        string path = Path.Combine(_tempDir, "manifest.mpd");
        File.WriteAllText(path, MinimalMpd, Encoding.UTF8);
        return Path.Combine(_tempDir, $"{title}.mpd");
    }

    private static OutputPlan CreatePlan(IReadOnlyList<ChapterInfo>? chapters) =>
        new(
            Format: OutputFormat.Dash,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    8000,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: chapters
        );
}
