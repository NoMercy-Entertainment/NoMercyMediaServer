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
            PlaylistGenerator.VideoVariantKey,
            _ => new VariantMetrics(5_000_000, 4_500_000)
        );
        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            PlaylistGenerator.AudioVariantKey,
            _ => new VariantMetrics(192_000, 180_000)
        );

        PlaylistGenerator gen = new();
        return gen.GenerateMasterPlaylist(plan, "Test.Movie", videoMetrics, audioMetrics);
    }

    // ----------------------------------------------------------------
    // One EXT-X-MEDIA per subtitle stream
    // ----------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_HasOneExtXMediaPerSubtitleStream()
    {
        OutputPlan plan = BuildPlan([
            Sub(SubtitleCodecType.WebVtt, "eng"),
            Sub(SubtitleCodecType.WebVtt, "fra"),
        ]);

        string playlist = GenerateMaster(plan);

        int tagCount = CountOccurrences(playlist, "TYPE=SUBTITLES");
        tagCount.Should().Be(2, "two subtitle streams → two EXT-X-MEDIA tags");
    }

    // ----------------------------------------------------------------
    // STREAM-INF includes SUBTITLES="subs" when subtitles present
    // ----------------------------------------------------------------

    [Fact]
    public void StreamInf_IncludesSubtitlesAttribute_WhenSubtitlesPresentAll()
    {
        OutputPlan plan = BuildPlan([Sub(SubtitleCodecType.WebVtt, "eng")]);

        string playlist = GenerateMaster(plan);

        playlist.Should().Contain("SUBTITLES=\"subs\"");
    }

    [Fact]
    public void StreamInf_NoSubtitlesAttribute_WhenNoSubtitles()
    {
        OutputPlan plan = BuildPlan([]);

        string playlist = GenerateMaster(plan);

        playlist.Should().NotContain("SUBTITLES=");
    }

    // ----------------------------------------------------------------
    // 3-source fixture: SRT + PGS (dropped) + ASS → 2 active EXT-X-MEDIA
    // ----------------------------------------------------------------

    [Fact]
    public void ThreeSourceFixture_SrtPgsAss_EmitsCorrectMediaTags()
    {
        // PGS with Drop action should NOT appear; SRT and ASS (Extract) should.
        SubtitleOutputPlan srt = new(
            SubtitleCodecType.WebVtt,
            StreamAction.Extract,
            "eng",
            0,
            "0:s:0"
        );

        SubtitleOutputPlan pgs = new(
            SubtitleCodecType.Pgs,
            StreamAction.Drop,
            "eng",
            1,
            "0:s:1"
        );

        SubtitleOutputPlan ass = new(
            SubtitleCodecType.Ass,
            StreamAction.Extract,
            "fra",
            2,
            "0:s:2"
        );

        OutputPlan plan = BuildPlan([srt, pgs, ass]);

        string playlist = GenerateMaster(plan);

        int tagCount = CountOccurrences(playlist, "TYPE=SUBTITLES");
        tagCount.Should().Be(2, "SRT and ASS are active; PGS is dropped and must not appear");

        playlist.Should().Contain("LANGUAGE=\"fra\"");
        playlist.Should().Contain("LANGUAGE=\"eng\"");
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
            SubtitleCodecType.Ass,
            StreamAction.Extract,
            "eng",
            0,
            "0:s:0"
        );

        string playlist = PlaylistGenerator.GenerateAssMediaPlaylist(sub, "subs_eng.ass", 6);

        playlist.Should().StartWith("#EXTM3U");
        playlist.Should().Contain("subs_eng.ass");
        playlist.Should().Contain("#EXT-X-ENDLIST");
        playlist.Should().Contain("#EXT-X-TARGETDURATION:6");
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
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        SubtitleOutputPlan sub = new(
            SubtitleCodecType.WebVtt,
            StreamAction.Extract,
            "eng",
            0,
            "0:s:0"
        );

        string playlist = PlaylistGenerator.GenerateSubtitleMediaPlaylist(sub, segments, 6);

        // Playlist lives in subtitles/eng/ — segment URIs are relative to it.
        playlist.Should().Contain("#EXTM3U");
        playlist.Should().Contain("full_00000.vtt");
        playlist.Should().Contain("full_00001.vtt");
        playlist.Should().Contain("#EXT-X-ENDLIST");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static OutputPlan BuildPlan(SubtitleOutputPlan[] subs) =>
        new(
            OutputFormat.Hls,
            [BuildVideo()],
            [BuildAudio()],
            subs,
            null
        );

    private static SubtitleOutputPlan Sub(SubtitleCodecType codec, string lang) =>
        new(
            codec,
            StreamAction.Extract,
            lang,
            0,
            "0:s:0"
        );

    private static VideoOutputPlan BuildVideo() =>
        new(
            1920,
            1080,
            "libx264",
            23,
            4000,
            "medium",
            "high",
            "4.1",
            false,
            "yuv420p",
            "[v0]",
            new()
        );

    private static AudioOutputPlan BuildAudio() =>
        new(
            "aac",
            192,
            2,
            48000,
            StreamAction.Transcode,
            "eng",
            "0:a:0"
        );

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
    }
}
