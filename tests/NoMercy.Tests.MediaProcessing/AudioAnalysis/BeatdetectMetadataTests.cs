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

using NoMercy.MediaProcessing.AudioAnalysis;

namespace NoMercy.Tests.MediaProcessing.AudioAnalysis;

/// <summary>
/// The beat grid as nomercy-ffmpeg v1.0.40 reports it: tempo, confidence, beat
/// interval and beat offset as frame metadata that <c>ametadata</c> prints.
/// <para>
/// Only the frame carrying <c>lavfi.beatdetect.final=1</c> holds the verdict.
/// Every earlier frame carries a running estimate that sits at half time for
/// most of the pass, so a parser that takes the last value it saw reports 64
/// BPM for a 128 BPM track.
/// </para>
/// <para>
/// Fixture: a generated 20 s 128 BPM click over a C major triad through the
/// production filter graph, captured from ffmpeg 9.0-NoMercy-MediaServer
/// (v1.0.40). Its stderr still carries the legacy bare tempo line and its
/// pre-final frames still carry 64.06, so reading 128.06 from it proves the
/// metadata verdict wins over both.
/// </para>
/// </summary>
public class BeatdetectMetadataTests
{
    private static string[] ReadFixture(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "AudioAnalysis", "Fixtures", name);
        return File.ReadAllLines(path);
    }

    private static AudioAnalysisResult ParseClickFixture()
    {
        AudioAnalysisOutputParser parser = new();

        foreach (string line in ReadFixture("v1040-click-128-stdout.txt"))
        {
            parser.ConsumeStdOut(line);
        }

        foreach (string line in ReadFixture("v1040-click-128-stderr.txt"))
        {
            parser.ConsumeStdErr(line);
        }

        return parser.Build();
    }

    [Fact]
    public void Fixture_ReadsTheTempoFromTheFinalFrame()
    {
        ParseClickFixture().Bpm.Should().BeApproximately(128.0, 0.5);
    }

    [Fact]
    public void Fixture_ReadsTheConfidenceTheFilterReports()
    {
        ParseClickFixture().BpmConfidence.Should().BeInRange(0.8, 1.0);
    }

    [Fact]
    public void Fixture_ReadsTheBeatGrid()
    {
        AudioAnalysisResult result = ParseClickFixture();

        result.BeatIntervalMs.Should().BeApproximately(468.75, 1.0);
        result.BeatOffsetMs.Should().NotBeNull();
    }

    [Fact]
    public void Fixture_MarksTheGridAsComingFromMetadata()
    {
        ParseClickFixture().BeatGridFromMetadata.Should().BeTrue();
    }

    /// <summary>
    /// The running estimate spends most of a pass an octave low. Taking the last
    /// frame instead of the final one would halve every tempo in the library.
    /// </summary>
    [Fact]
    public void TheRunningEstimateIsIgnoredInFavourOfTheFinalFrame()
    {
        AudioAnalysisOutputParser parser = new();

        foreach (string line in RunningEstimateThenFinalFrame())
        {
            parser.ConsumeStdOut(line);
        }

        AudioAnalysisResult result = parser.Build();

        result.Bpm.Should().Be(128.06);
        result.BpmConfidence.Should().Be(0.891);
        result.BeatIntervalMs.Should().Be(468.53);
        result.BeatOffsetMs.Should().Be(468);
        result.BeatGridFromMetadata.Should().BeTrue();
    }

    private static string[] RunningEstimateThenFinalFrame()
    {
        return
        [
            "frame:0    pts:0       pts_time:0",
            "lavfi.beatdetect.bpm=64.03",
            "lavfi.beatdetect.confidence=0.910",
            "lavfi.beatdetect.final=0",
            "frame:1    pts:1024    pts_time:0.02322",
            "lavfi.beatdetect.bpm=64.03",
            "lavfi.beatdetect.confidence=0.910",
            "lavfi.beatdetect.final=0",
            "frame:2    pts:2048    pts_time:0.0464399",
            "lavfi.beatdetect.bpm=128.06",
            "lavfi.beatdetect.confidence=0.891",
            "lavfi.beatdetect.beat_interval_ms=468.53",
            "lavfi.beatdetect.beat_offset_ms=467.8",
            "lavfi.beatdetect.final=1",
        ];
    }

    /// <summary>
    /// An ffmpeg build older than v1.0.40 prints no beatdetect metadata at all,
    /// only the tagged stderr line. The tempo still lands; nothing else is
    /// invented, and the flag says the grid was not measured.
    /// </summary>
    [Fact]
    public void AnOlderBuildFallsBackToTheStderrLine()
    {
        AudioAnalysisOutputParser parser = new();

        parser.ConsumeStdOut("frame:0    pts:0       pts_time:0");
        parser.ConsumeStdOut("lavfi.keydetect.key=C");
        parser.ConsumeStdErr(
            "[Parsed_beatdetect_0 @ 000001a2b1854000] lavfi.beatdetect.bpm=112.65 "
        );

        AudioAnalysisResult result = parser.Build();

        result.Bpm.Should().Be(112.65);
        result.BpmConfidence.Should().BeNull();
        result.BeatIntervalMs.Should().BeApproximately(532.624, 0.001);
        result.BeatOffsetMs.Should().BeNull();
        result.BeatGridFromMetadata.Should().BeFalse();
    }

    [Fact]
    public void NeitherRouteLeavesTheWholeGridUnset()
    {
        AudioAnalysisOutputParser parser = new();

        parser.ConsumeStdOut("frame:0    pts:0       pts_time:0");
        parser.ConsumeStdOut("lavfi.keydetect.key=C");

        AudioAnalysisResult result = parser.Build();

        result.Bpm.Should().BeNull();
        result.BpmConfidence.Should().BeNull();
        result.BeatIntervalMs.Should().BeNull();
        result.BeatOffsetMs.Should().BeNull();
        result.BeatGridFromMetadata.Should().BeFalse();
    }

    /// <summary>
    /// The filter prints 0.00 when it locked onto nothing. A final frame saying
    /// that is an absence, not a tempo of zero and not a measured grid.
    /// </summary>
    [Fact]
    public void AFinalFrameWithoutATempoIsAnAbsence()
    {
        AudioAnalysisOutputParser parser = new();

        parser.ConsumeStdOut("frame:0    pts:0       pts_time:0");
        parser.ConsumeStdOut("lavfi.beatdetect.bpm=0.00");
        parser.ConsumeStdOut("lavfi.beatdetect.confidence=0.000");
        parser.ConsumeStdOut("lavfi.beatdetect.final=1");

        AudioAnalysisResult result = parser.Build();

        result.Bpm.Should().BeNull();
        result.BpmConfidence.Should().BeNull();
        result.BeatGridFromMetadata.Should().BeFalse();
    }
}
