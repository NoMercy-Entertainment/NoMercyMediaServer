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

using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Strategies.Hls;

/// <summary>
/// Verifies that the master playlist correctly emits EXT-X-MEDIA TYPE=SUBTITLES
/// tags and wires SUBTITLES="subs" onto each EXT-X-STREAM-INF line.
/// </summary>
public class HlsSubtitleMediaTagTests
{
    private static string GenerateMaster(OutputPlan plan)
    {
        // Seed non-zero metrics so the variant rows render — GenerateMasterPlaylist
        // skips any variant whose measured bandwidth is zero (dead-variant guard).
        // Metrics are keyed by the resolved playlist path (VideoVariantKey /
        // AudioVariantKey), not MapLabel — every rung re-plans as MapLabel "[v0]"
        // in its own bundle, so keying by label collapsed the ladder.
        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: PlaylistGenerator.VideoVariantKey,
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );
        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            keySelector: PlaylistGenerator.AudioVariantKey,
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
        );

        PlaylistGenerator gen = new();
        return gen.GenerateMasterPlaylist(plan: plan, mediaTitle: "Test.Movie", videoMetrics: videoMetrics, audioMetrics: audioMetrics);
    }

    // ----------------------------------------------------------------
    // One EXT-X-MEDIA per subtitle stream
    // ----------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_HasOneExtXMediaPerSubtitleStream()
    {
        OutputPlan plan = BuildPlan(subs:
        [
            Sub(codec: SubtitleCodecType.WebVtt, lang: "eng"),
            Sub(codec: SubtitleCodecType.WebVtt, lang: "fra"),
        ]);

        string playlist = GenerateMaster(plan: plan);

        int tagCount = CountOccurrences(haystack: playlist, needle: "TYPE=SUBTITLES");
        tagCount.Should().Be(expected: 2, because: "two subtitle streams → two EXT-X-MEDIA tags");
    }

    // ----------------------------------------------------------------
    // STREAM-INF includes SUBTITLES="subs" when subtitles present
    // ----------------------------------------------------------------

    [Fact]
    public void StreamInf_IncludesSubtitlesAttribute_WhenSubtitlesPresentAll()
    {
        OutputPlan plan = BuildPlan(subs: [Sub(codec: SubtitleCodecType.WebVtt, lang: "eng")]);

        string playlist = GenerateMaster(plan: plan);

        playlist.Should().Contain(expected: "SUBTITLES=\"subs\"");
    }

    [Fact]
    public void StreamInf_NoSubtitlesAttribute_WhenNoSubtitles()
    {
        OutputPlan plan = BuildPlan(subs: []);

        string playlist = GenerateMaster(plan: plan);

        playlist.Should().NotContain(unexpected: "SUBTITLES=");
    }

    // ----------------------------------------------------------------
    // 3-source fixture: SRT + PGS (dropped) + ASS → 2 active EXT-X-MEDIA
    // ----------------------------------------------------------------

    [Fact]
    public void ThreeSourceFixture_SrtPgsAss_EmitsCorrectMediaTags()
    {
        // PGS with Drop action should NOT appear; SRT and ASS (Extract) should.
        SubtitleOutputPlan srt = new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        SubtitleOutputPlan pgs = new(
            OutputCodec: SubtitleCodecType.Pgs,
            Action: StreamAction.Drop,
            Language: "eng",
            SourceIndex: 1,
            MapLabel: "0:s:1"
        );

        SubtitleOutputPlan ass = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "fra",
            SourceIndex: 2,
            MapLabel: "0:s:2"
        );

        OutputPlan plan = BuildPlan(subs: [srt, pgs, ass]);

        string playlist = GenerateMaster(plan: plan);

        int tagCount = CountOccurrences(haystack: playlist, needle: "TYPE=SUBTITLES");
        tagCount.Should().Be(expected: 2, because: "SRT and ASS are active; PGS is dropped and must not appear");

        playlist.Should().Contain(expected: "LANGUAGE=\"fra\"");
        playlist.Should().Contain(expected: "LANGUAGE=\"eng\"");
    }

    // ----------------------------------------------------------------
    // WebVttSegmenter media playlist — font copy scenario (unit)
    // ----------------------------------------------------------------

    [Fact]
    public void SubtitleMediaPlaylist_FontCopy_SegmentsReferenceCorrectUris()
    {
        // Simulate font files alongside .ass sidecar: the playlist generator
        // emits one entry per segment. Font presence is verified by the caller
        // copying font files to the output dir; the playlist itself doesn't
        // list fonts. This test validates the ASS playlist format.
        SubtitleOutputPlan sub = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        string playlist = PlaylistGenerator.GenerateAssMediaPlaylist(sub: sub, assFileName: "subs_eng.ass", segmentDurationSeconds: 6);

        playlist.Should().StartWith(expected: "#EXTM3U");
        playlist.Should().Contain(expected: "subs_eng.ass");
        playlist.Should().Contain(expected: "#EXT-X-ENDLIST");
        playlist.Should().Contain(expected: "#EXT-X-TARGETDURATION:6");
    }

    // ----------------------------------------------------------------
    // WebVttSegmenter media playlist for VTT segments
    // ----------------------------------------------------------------

    [Fact]
    public void SubtitleMediaPlaylist_VttSegments_ContainsCorrectEntries()
    {
        WebVttSegmenter segmenter = new();
        string vttContent =
            "WEBVTT\n\n00:00:01.000 --> 00:00:03.000\nHello\n\n"
            + "00:00:07.000 --> 00:00:08.000\nWorld\n";

        IReadOnlyList<WebVttSegment> segments = segmenter.SliceContent(
            vttContent: vttContent,
            segmentDuration: TimeSpan.FromSeconds(seconds: 6)
        );

        SubtitleOutputPlan sub = new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        string playlist = PlaylistGenerator.GenerateSubtitleMediaPlaylist(sub: sub, segments: segments, segmentDurationSeconds: 6);

        // Playlist lives in subtitles/eng/ — segment URIs are relative to it.
        playlist.Should().Contain(expected: "#EXTM3U");
        playlist.Should().Contain(expected: "full_00000.vtt");
        playlist.Should().Contain(expected: "full_00001.vtt");
        playlist.Should().Contain(expected: "#EXT-X-ENDLIST");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static OutputPlan BuildPlan(SubtitleOutputPlan[] subs) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs: subs,
            Thumbnails: null
        );

    private static SubtitleOutputPlan Sub(SubtitleCodecType codec, string lang) =>
        new(
            OutputCodec: codec,
            Action: StreamAction.Extract,
            Language: lang,
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

    private static VideoOutputPlan BuildVideo() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: new()
        );

    private static AudioOutputPlan BuildAudio() =>
        new(
            EncoderName: "aac",
            BitrateKbps: 192,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: "eng",
            MapLabel: "0:a:0"
        );

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(value: needle, startIndex: idx, comparisonType: StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
    }
}
