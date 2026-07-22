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
        string cues = string.Join(separator: "\n\n", value: cueBlocks);
        return string.IsNullOrEmpty(value: cues) ? "WEBVTT\n" : $"WEBVTT\n\n{cues}\n";
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
        string vttContent = Vtt(cueBlocks: Cue(start: "00:00:01.000", end: "00:00:03.000", text: "Hello"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 1, because: "cue ends at 3 s, one 6-second segment covers it");
        segments[index: 0].Content.Should().Contain(expected: "Hello");
        segments[index: 0].Index.Should().Be(expected: 0);
    }

    [Fact]
    public void SingleCue_InsideSecondSegment_OnlyInSegmentOne()
    {
        // Cue at 7s–9s; segment duration = 6s → segment 1 covers [6,12).
        string vttContent = Vtt(cueBlocks: Cue(start: "00:00:07.000", end: "00:00:09.000", text: "World"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 2);
        segments[index: 0].Content.Should().NotContain(unexpected: "World", because: "cue is in segment 1, not 0");
        segments[index: 1].Content.Should().Contain(expected: "World");
    }

    // ----------------------------------------------------------------
    // Cue straddling a boundary → duplicated into both segments
    // ----------------------------------------------------------------

    [Fact]
    public void CueStraddlingBoundary_DuplicatedIntoBothSegments()
    {
        // Cue at 5s–7s straddles the 6 s boundary → in segments 0 and 1.
        string vttContent = Vtt(cueBlocks: Cue(start: "00:00:05.000", end: "00:00:07.000", text: "Straddle"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 2);
        segments[index: 0].Content.Should().Contain(expected: "Straddle", because: "cue overlaps segment 0 [0,6)");
        segments[index: 1].Content.Should().Contain(expected: "Straddle", because: "cue overlaps segment 1 [6,12)");
    }

    // ----------------------------------------------------------------
    // Empty input file → single empty segment with WEBVTT header
    // ----------------------------------------------------------------

    [Fact]
    public void EmptyInputFile_SingleSegmentWithWebVttHeader()
    {
        string vttContent = "WEBVTT\n";

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 1);
        segments[index: 0].Content.Should().StartWith(expected: "WEBVTT");
        segments[index: 0].Content.Should().NotContain(unexpected: "-->", because: "empty input has no cue timestamps");
    }

    // ----------------------------------------------------------------
    // Input with X-TIMESTAMP-MAP → preserved per output segment
    // ----------------------------------------------------------------

    [Fact]
    public void InputWithTimestampMap_PreservedInOutput()
    {
        string vttContent =
            "WEBVTT\nX-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000\n\n"
            + Cue(start: "00:00:01.000", end: "00:00:03.000", text: "Mapped");

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 1);
        segments[index: 0].Content.Should().Contain(expected: "X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000");
    }

    [Fact]
    public void InputWithoutTimestampMap_StandardMapInjected()
    {
        string vttContent = Vtt(cueBlocks: Cue(start: "00:00:01.000", end: "00:00:02.000", text: "Plain"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments[index: 0].Content.Should().Contain(expected: "X-TIMESTAMP-MAP=MPEGTS:0,LOCAL:00:00:00.000");
    }

    // ----------------------------------------------------------------
    // Segment index and time boundaries
    // ----------------------------------------------------------------

    [Fact]
    public void SegmentIndexAndBoundaries_AreCorrect()
    {
        string vttContent = Vtt(cueBlocks: [Cue(start: "00:00:01.000", end: "00:00:02.000", text: "A"), Cue(start: "00:00:07.000", end: "00:00:08.000", text: "B"), Cue(start: "00:00:13.000", end: "00:00:14.000", text: "C")]
        );

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 3);
        segments[index: 0].Index.Should().Be(expected: 0);
        segments[index: 1].Index.Should().Be(expected: 1);
        segments[index: 2].Index.Should().Be(expected: 2);

        segments[index: 0].StartTime.Should().Be(expected: TimeSpan.Zero);
        segments[index: 0].EndTime.Should().Be(expected: TimeSpan.FromSeconds(seconds: 6));
        segments[index: 1].StartTime.Should().Be(expected: TimeSpan.FromSeconds(seconds: 6));
        segments[index: 2].StartTime.Should().Be(expected: TimeSpan.FromSeconds(seconds: 12));
    }

    // ── argument validation ────────────────────────────────────────────────

    [Fact]
    public void SliceContent_ZeroSegmentDuration_Throws()
    {
        // Zero would divide by zero downstream — guard the entry point.
        Action act = () => _sut.SliceContent(vttContent: Vtt(), segmentDuration: TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(paramName: "segmentDuration");
    }

    [Fact]
    public void SliceContent_NegativeSegmentDuration_Throws()
    {
        Action act = () => _sut.SliceContent(vttContent: Vtt(), segmentDuration: TimeSpan.FromSeconds(seconds: -1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(paramName: "segmentDuration");
    }

    // ── multiple cues in one segment ───────────────────────────────────────

    [Fact]
    public void MultipleCues_InSameSegment_BothPresent()
    {
        // Two cues fully inside segment 0 — both must land together.
        string vttContent = Vtt(cueBlocks: [Cue(start: "00:00:01.000", end: "00:00:02.000", text: "First"), Cue(start: "00:00:03.000", end: "00:00:04.000", text: "Second")]
        );

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 1);
        segments[index: 0].Content.Should().Contain(expected: "First");
        segments[index: 0].Content.Should().Contain(expected: "Second");
    }

    // ── hour-format timestamps ─────────────────────────────────────────────

    [Fact]
    public void HourFormatTimestamps_ParseCorrectly()
    {
        // VTT supports both HH:MM:SS.mmm and MM:SS.mmm. The regex accepts
        // optional hours — a long movie cue at 1h:00:01 must parse.
        string vttContent = Vtt(cueBlocks: Cue(start: "01:00:01.000", end: "01:00:03.000", text: "Late"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        // 1h = 3600s → segment 600 covers [3600, 3606); cue at 3601s lands there.
        segments.Should().HaveCountGreaterThan(expected: 599);
        segments[index: 600].Content.Should().Contain(expected: "Late");
    }

    // ── cue exactly on boundary ────────────────────────────────────────────

    [Fact]
    public void CueEndingExactlyOnBoundary_OnlyInPriorSegment()
    {
        // Cue end == segment end → overlap predicate is `c.End > segStart`,
        // so cue does NOT carry into the next segment.
        string vttContent = Vtt(cueBlocks: Cue(start: "00:00:04.000", end: "00:00:06.000", text: "BoundaryEnd"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 1, because: "cue ends exactly at the 6 s boundary");
        segments[index: 0].Content.Should().Contain(expected: "BoundaryEnd");
    }

    [Fact]
    public void CueStartingExactlyOnBoundary_OnlyInNextSegment()
    {
        // Cue starts at 6s exactly → only in segment 1, not segment 0.
        string vttContent = Vtt(cueBlocks: Cue(start: "00:00:06.000", end: "00:00:08.000", text: "BoundaryStart"));

        IReadOnlyList<WebVttSegment> segments = _sut.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        segments.Should().HaveCount(expected: 2);
        segments[index: 0].Content.Should().NotContain(unexpected: "BoundaryStart");
        segments[index: 1].Content.Should().Contain(expected: "BoundaryStart");
    }
}
