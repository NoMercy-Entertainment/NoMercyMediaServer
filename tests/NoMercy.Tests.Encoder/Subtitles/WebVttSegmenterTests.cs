using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

public class WebVttSegmenterTests
{
    private readonly WebVttSegmenter _sut = new();

    // ----------------------------------------------------------------
    // Helper to build a minimal WebVTT string
    // ----------------------------------------------------------------

    private static string Vtt(params string[] cueBlocks)
    {
        string cues = string.Join("\n\n", cueBlocks);
        return string.IsNullOrEmpty(cues) ? "WEBVTT\n" : $"WEBVTT\n\n{cues}\n";
    }

    private static string Cue(string start, string end, string text) =>
        $"{start} --> {end}\n{text}";

    // ----------------------------------------------------------------
    // Single cue inside one segment
    // ----------------------------------------------------------------

    [Fact]
    public void SingleCue_InsideOneSegment_GoesOnlyToThatSegment()
    {
        // Cue covers 1s–3s; segment duration = 6s → falls entirely in segment 0.
        string vttContent = Vtt(Cue("00:00:01.000", "00:00:03.000", "Hello"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments.Should().HaveCount(1, "cue ends at 3 s, one 6-second segment covers it");
        segments[0].Content.Should().Contain("Hello");
        segments[0].Index.Should().Be(0);
    }

    [Fact]
    public void SingleCue_InsideSecondSegment_OnlyInSegmentOne()
    {
        // Cue at 7s–9s; segment duration = 6s → segment 1 covers [6,12).
        string vttContent = Vtt(Cue("00:00:07.000", "00:00:09.000", "World"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments.Should().HaveCount(2);
        segments[0].Content.Should().NotContain("World", "cue is in segment 1, not 0");
        segments[1].Content.Should().Contain("World");
    }

    // ----------------------------------------------------------------
    // Cue straddling a boundary → duplicated into both segments
    // ----------------------------------------------------------------

    [Fact]
    public void CueStraddlingBoundary_DuplicatedIntoBothSegments()
    {
        // Cue at 5s–7s straddles the 6 s boundary → in segments 0 and 1.
        string vttContent = Vtt(Cue("00:00:05.000", "00:00:07.000", "Straddle"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments.Should().HaveCount(2);
        segments[0].Content.Should().Contain("Straddle", "cue overlaps segment 0 [0,6)");
        segments[1].Content.Should().Contain("Straddle", "cue overlaps segment 1 [6,12)");
    }

    // ----------------------------------------------------------------
    // Empty input file → single empty segment with WEBVTT header
    // ----------------------------------------------------------------

    [Fact]
    public void EmptyInputFile_SingleSegmentWithWebVttHeader()
    {
        string vttContent = "WEBVTT\n";

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments.Should().HaveCount(1);
        segments[0].Content.Should().StartWith("WEBVTT");
        segments[0].Content.Should().NotContain("-->", "empty input has no cue timestamps");
    }

    // ----------------------------------------------------------------
    // Input with X-TIMESTAMP-MAP → preserved per output segment
    // ----------------------------------------------------------------

    [Fact]
    public void InputWithTimestampMap_PreservedInOutput()
    {
        string vttContent =
            "WEBVTT\nX-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000\n\n"
            + Cue("00:00:01.000", "00:00:03.000", "Mapped");

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments.Should().HaveCount(1);
        segments[0].Content.Should().Contain("X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000");
    }

    [Fact]
    public void InputWithoutTimestampMap_StandardMapInjected()
    {
        string vttContent = Vtt(Cue("00:00:01.000", "00:00:02.000", "Plain"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments[0].Content.Should().Contain("X-TIMESTAMP-MAP=MPEGTS:0,LOCAL:00:00:00.000");
    }

    // ----------------------------------------------------------------
    // Segment index and time boundaries
    // ----------------------------------------------------------------

    [Fact]
    public void SegmentIndexAndBoundaries_AreCorrect()
    {
        string vttContent = Vtt(
            Cue("00:00:01.000", "00:00:02.000", "A"),
            Cue("00:00:07.000", "00:00:08.000", "B"),
            Cue("00:00:13.000", "00:00:14.000", "C")
        );

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        segments.Should().HaveCount(3);
        segments[0].Index.Should().Be(0);
        segments[1].Index.Should().Be(1);
        segments[2].Index.Should().Be(2);

        segments[0].StartTime.Should().Be(TimeSpan.Zero);
        segments[0].EndTime.Should().Be(TimeSpan.FromSeconds(6));
        segments[1].StartTime.Should().Be(TimeSpan.FromSeconds(6));
        segments[2].StartTime.Should().Be(TimeSpan.FromSeconds(12));
    }
}
