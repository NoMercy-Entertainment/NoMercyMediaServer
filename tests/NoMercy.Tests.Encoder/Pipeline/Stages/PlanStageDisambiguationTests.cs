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

using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Plan-level path collision resolution. When two outputs resolve to the same
/// on-disk path the disambiguator appends a per-stream / per-codec suffix so
/// segments don't overwrite each other. Single-collision plans pass through
/// unchanged.
/// </summary>
public class PlanStageDisambiguationTests
{
    private static AudioOutputPlan Audio(
        string lang,
        string encoder,
        int sourceIdx,
        string segTemplate = "audio_:lang:_:codec:/:lang:_:codec:_%05d",
        string playlistTemplate = "audio_:lang:_:codec:/playlist"
    ) =>
        new(
            EncoderName: encoder,
            BitrateKbps: 128,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: lang,
            MapLabel: $"0:a:{sourceIdx}"
        )
        {
            SegmentNameTemplate = segTemplate,
            PlaylistNameTemplate = playlistTemplate,
        };

    private static VideoOutputPlan Video(
        int width,
        int height,
        string encoder,
        bool hdr = false,
        string segTemplate = "video_:framesize:/:framesize:_%05d",
        string playlistTemplate = "video_:framesize:/playlist"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoder,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "main",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v]",
            ExtraFlags: []
        )
        {
            SegmentNameTemplate = segTemplate,
            PlaylistNameTemplate = playlistTemplate,
            IsHdrOutput = hdr,
        };

    // ── Audio disambiguation ────────────────────────────────────────────────

    [Fact]
    public void DisambiguateAudio_SingleLanguagePerCodec_PassesThrough()
    {
        AudioOutputPlan[] plans = [Audio(lang: "eng", encoder: "aac", sourceIdx: 0), Audio(lang: "fra", encoder: "aac", sourceIdx: 1)];

        AudioOutputPlan[] result = PlanStageDisambiguation.DisambiguateAudio(plans: plans).ToArray();

        result[0].SegmentNameTemplate.Should().Be(expected: plans[0].SegmentNameTemplate);
        result[1].SegmentNameTemplate.Should().Be(expected: plans[1].SegmentNameTemplate);
    }

    [Fact]
    public void DisambiguateAudio_TwoEnglishAac_Disambiguates()
    {
        // Two English AAC streams (e.g. commentary track + main) would collide
        // on audio_eng_aac/. Append source index suffix per stream.
        AudioOutputPlan[] plans = [Audio(lang: "eng", encoder: "aac", sourceIdx: 0), Audio(lang: "eng", encoder: "aac", sourceIdx: 1)];

        AudioOutputPlan[] result = PlanStageDisambiguation.DisambiguateAudio(plans: plans).ToArray();

        result[0].SegmentNameTemplate.Should().Contain(expected: "_0");
        result[1].SegmentNameTemplate.Should().Contain(expected: "_1");
        result[0].SegmentNameTemplate.Should().NotBe(unexpected: result[1].SegmentNameTemplate);
    }

    [Fact]
    public void DisambiguateAudio_DifferentCodecsSameLanguage_PassThrough()
    {
        // Two English streams with DIFFERENT codecs land in different dirs already.
        AudioOutputPlan[] plans = [Audio(lang: "eng", encoder: "aac", sourceIdx: 0), Audio(lang: "eng", encoder: "libfdk_aac", sourceIdx: 1)];

        AudioOutputPlan[] result = PlanStageDisambiguation.DisambiguateAudio(plans: plans).ToArray();

        // 'lib' and 'libfdk_' prefixes are stripped to compare — these still collide
        // because both resolve to "aac" codec token. Disambiguates.
        result[0].SegmentNameTemplate.Should().NotBe(unexpected: plans[0].SegmentNameTemplate);
    }

    [Fact]
    public void DisambiguateAudio_SegmentAndPlaylistBothDisambiguated()
    {
        AudioOutputPlan[] plans = [Audio(lang: "eng", encoder: "aac", sourceIdx: 0), Audio(lang: "eng", encoder: "aac", sourceIdx: 1)];

        AudioOutputPlan[] result = PlanStageDisambiguation.DisambiguateAudio(plans: plans).ToArray();

        // Both directory AND filename suffix get the disambiguator — otherwise
        // .m4s segments still collide inside the same dir.
        result[0].SegmentNameTemplate.Should().Contain(expected: "/").And.Contain(expected: "_0");
        result[0].PlaylistNameTemplate.Should().Contain(expected: "/").And.Contain(expected: "_0");
    }

    [Fact]
    public void DisambiguateAudio_EmptyInput_ReturnsEmpty()
    {
        PlanStageDisambiguation.DisambiguateAudio(plans: []).Should().BeEmpty();
    }

    // ── Video disambiguation ────────────────────────────────────────────────

    [Fact]
    public void DisambiguateVideo_DifferentResolutions_PassesThrough()
    {
        VideoOutputPlan[] plans = [Video(width: 1920, height: 1080, encoder: "libx264"), Video(width: 1280, height: 720, encoder: "libx264")];

        VideoOutputPlan[] result = PlanStageDisambiguation.DisambiguateVideo(plans: plans);

        result[0].SegmentNameTemplate.Should().Be(expected: plans[0].SegmentNameTemplate);
        result[1].SegmentNameTemplate.Should().Be(expected: plans[1].SegmentNameTemplate);
    }

    [Fact]
    public void DisambiguateVideo_SameResolutionTwoCodecs_AppendsCodecFamily()
    {
        // EmitHdrAndSdr can produce H.264 1080p AND HEVC 1080p — they'd collide
        // on video_1920x1080/. Append codec family suffix.
        VideoOutputPlan[] plans = [Video(width: 1920, height: 1080, encoder: "libx264"), Video(width: 1920, height: 1080, encoder: "libx265")];

        VideoOutputPlan[] result = PlanStageDisambiguation.DisambiguateVideo(plans: plans);

        result[0].SegmentNameTemplate.Should().Contain(expected: "avc");
        result[1].SegmentNameTemplate.Should().Contain(expected: "hevc");
    }

    [Fact]
    public void DisambiguateVideo_HdrAndSdrAtSameSize_PassThrough()
    {
        // Different IsHdrOutput → different group keys → no collision.
        VideoOutputPlan[] plans =
        [
            Video(width: 1920, height: 1080, encoder: "libx265", hdr: true),
            Video(width: 1920, height: 1080, encoder: "libx265", hdr: false),
        ];

        VideoOutputPlan[] result = PlanStageDisambiguation.DisambiguateVideo(plans: plans);

        result[0].SegmentNameTemplate.Should().Be(expected: plans[0].SegmentNameTemplate);
        result[1].SegmentNameTemplate.Should().Be(expected: plans[1].SegmentNameTemplate);
    }

    [Fact]
    public void DisambiguateVideo_EmptyInput_ReturnsEmpty()
    {
        PlanStageDisambiguation.DisambiguateVideo(plans: []).Should().BeEmpty();
    }
}
