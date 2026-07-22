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

using Newtonsoft.Json.Linq;

namespace NoMercy.Tests.Encoder.Fidelity;

/// <summary>
/// Proves the oracle itself is correct: every defect class it guards against
/// must fire on a bad file and stay silent on a good one. Deterministic — builds
/// synthetic <see cref="ProbedMedia"/> so no ffmpeg is needed. If these pass, a
/// green oracle on a real encode actually means something.
/// </summary>
public class EncodeFidelityOracleTests
{
    // ── fixture builders ───────────────────────────────────────────────────────

    private static JObject VideoStream(
        string codecName = "hevc",
        string codecTag = "hvc1",
        string transfer = "smpte2084",
        string primaries = "bt2020",
        string space = "bt2020nc",
        string pixFmt = "yuv420p10le"
    ) =>
        new()
        {
            [propertyName: "codec_type"] = "video",
            [propertyName: "codec_name"] = codecName,
            [propertyName: "codec_tag_string"] = codecTag,
            [propertyName: "color_transfer"] = transfer,
            [propertyName: "color_primaries"] = primaries,
            [propertyName: "color_space"] = space,
            [propertyName: "pix_fmt"] = pixFmt,
        };

    private static JObject AudioStream(
        string codecName = "eac3",
        string layout = "5.1(side)",
        string language = "eng",
        int isDefault = 1
    ) =>
        new()
        {
            [propertyName: "codec_type"] = "audio",
            [propertyName: "codec_name"] = codecName,
            [propertyName: "channel_layout"] = layout,
            [propertyName: "disposition"] = new JObject { [propertyName: "default"] = isDefault },
            [propertyName: "tags"] = new JObject { [propertyName: "language"] = language },
        };

    private static JObject SubStream(string codecName = "mov_text", string language = "eng") =>
        new()
        {
            [propertyName: "codec_type"] = "subtitle",
            [propertyName: "codec_name"] = codecName,
            [propertyName: "tags"] = new JObject { [propertyName: "language"] = language },
        };

    private static JObject MasteringDisplay() =>
        new() { [propertyName: "side_data_type"] = "Mastering display metadata" };

    private static JObject DoviRecord() =>
        new() { [propertyName: "side_data_type"] = "DOVI configuration record", [propertyName: "rpu_present_flag"] = 1 };

    private static ProbedMedia Media(
        IEnumerable<JObject>? streams = null,
        IEnumerable<JObject>? sideData = null,
        IEnumerable<JObject>? chapters = null
    ) =>
        new()
        {
            Path = "synthetic",
            Streams = (streams ?? [VideoStream()]).ToList(),
            Format = new JObject(),
            Chapters = (chapters ?? []).ToList(),
            FirstFrameSideData = (sideData ?? [MasteringDisplay()]).ToList(),
        };

    // ── Dolby Vision tag ↔ RPU coherence (the corruption) ──────────────────────

    [Fact]
    public void DvTag_WithoutRpu_IsFlagged()
    {
        // The exact Punisher bug: dvh1 tag, no DOVI record.
        ProbedMedia output = Media(
            streams: [VideoStream(codecTag: "dvh1"), AudioStream()],
            sideData: [MasteringDisplay()] // no DOVI record
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckDolbyVisionTagCoherence(output: output, violations: violations);

        violations.Should().ContainSingle().Which.Should().Contain(expected: "DV-tag-without-RPU");
    }

    [Fact]
    public void DvTag_WithRpu_IsClean()
    {
        ProbedMedia output = Media(
            streams: [VideoStream(codecTag: "dvh1")],
            sideData: [MasteringDisplay(), DoviRecord()]
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckDolbyVisionTagCoherence(output: output, violations: violations);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Hvc1Tag_NoRpu_IsClean()
    {
        // A re-encoded HDR10 stream correctly tagged hvc1 — the fixed output.
        ProbedMedia output = Media(
            streams: [VideoStream(codecTag: "hvc1")],
            sideData: [MasteringDisplay()]
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckDolbyVisionTagCoherence(output: output, violations: violations);

        violations.Should().BeEmpty();
    }

    // ── HDR10 signalling ───────────────────────────────────────────────────────

    [Fact]
    public void Hdr10_MissingMasteringDisplay_IsFlagged()
    {
        ProbedMedia output = Media(streams: [VideoStream()], sideData: []); // PQ but no MDCV

        List<string> violations = [];
        EncodeFidelityOracle.CheckHdr10Signaling(output: output, violations: violations);

        violations.Should().ContainSingle().Which.Should().Contain(expected: "HDR10-mastering-display");
    }

    [Fact]
    public void Hdr10_WrongPrimaries_IsFlagged()
    {
        ProbedMedia output = Media(
            streams: [VideoStream(primaries: "bt709")],
            sideData: [MasteringDisplay()]
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckHdr10Signaling(output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("HDR10-primaries"));
    }

    [Fact]
    public void Hdr10_Complete_IsClean()
    {
        List<string> violations = [];
        EncodeFidelityOracle.CheckHdr10Signaling(output: Media(), violations: violations);
        violations.Should().BeEmpty();
    }

    // ── hvc1 vs hev1 ───────────────────────────────────────────────────────────

    [Fact]
    public void Hev1Tag_IsFlagged()
    {
        ProbedMedia output = Media(streams: [VideoStream(codecTag: "hev1")]);
        List<string> violations = [];
        EncodeFidelityOracle.CheckHevcFmp4Tag(output: output, violations: violations);
        violations.Should().ContainSingle().Which.Should().Contain(expected: "HEVC-tag-hev1");
    }

    // ── HLS master playlist ────────────────────────────────────────────────────

    [Fact]
    public void Master_IdenticalBandwidth_IsFlagged()
    {
        // The reported bug: both variants advertise the same BANDWIDTH.
        string master =
            "#EXTM3U\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=11084402,RESOLUTION=3840x2160,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=PQ\nv4k.m3u8\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=11084402,RESOLUTION=1920x1080,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=SDR\nv1080.m3u8\n";

        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(masterPlaylistText: master, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("HLS-identical-bandwidth"));
    }

    [Fact]
    public void Master_DistinctBandwidth_IsClean()
    {
        string master =
            "#EXTM3U\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=11084402,RESOLUTION=3840x2160,CODECS=\"hvc1.2.4.L150.B0\",VIDEO-RANGE=PQ\nv4k.m3u8\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=4192000,RESOLUTION=1920x1080,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=SDR\nv1080.m3u8\n";

        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(masterPlaylistText: master, violations: violations);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Master_HevcLevelTooLowForResolution_IsFlagged()
    {
        // The exact Punisher master: a 4K variant advertising HEVC level 4.0
        // (L120), which cannot legally carry 3840×2160.
        string master =
            "#EXTM3U\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=11084402,RESOLUTION=3840x2160,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=PQ\nv4k.m3u8\n";

        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(masterPlaylistText: master, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("HLS-codecs-level-too-low"));
    }

    [Fact]
    public void Master_HevcLevelCorrectForResolution_IsClean()
    {
        // 4K at L150 and 1080p at L120 are both legal — no level violation.
        string master =
            "#EXTM3U\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=11084402,RESOLUTION=3840x2160,CODECS=\"hvc1.2.4.L150.B0\",VIDEO-RANGE=PQ\nv4k.m3u8\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=4192000,RESOLUTION=1920x1080,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=SDR\nv1080.m3u8\n";

        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(masterPlaylistText: master, violations: violations);

        violations.Should().NotContain(predicate: v => v.Contains("HLS-codecs-level-too-low"));
    }

    [Fact]
    public void Master_MissingCodecsAndVideoRange_IsFlagged()
    {
        string master =
            "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080\nv.m3u8\n";
        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(masterPlaylistText: master, violations: violations);
        violations.Should().Contain(predicate: v => v.Contains("HLS-missing-codecs"));
        violations.Should().Contain(predicate: v => v.Contains("HLS-missing-video-range"));
    }

    // ── audio fidelity ─────────────────────────────────────────────────────────

    [Fact]
    public void Audio_DroppedTrack_IsFlagged()
    {
        // The Punisher case: source has 2 English audio tracks, output has 1.
        ProbedMedia source = Media(
            streams:
            [
                VideoStream(),
                AudioStream(language: "eng"),
                AudioStream(language: "eng", isDefault: 0),
            ]
        );
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(language: "eng")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source: source, output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("audio-tracks-dropped"));
    }

    [Fact]
    public void Audio_LanguageStripped_IsFlagged()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream(language: "eng")]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(language: "und")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source: source, output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("audio-language-stripped"));
    }

    [Fact]
    public void Audio_ChannelLayoutChangedOnCopy_IsFlagged()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream(layout: "5.1(side)")]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(layout: "5.1")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source: source, output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("audio-channel-layout"));
    }

    [Fact]
    public void Audio_Preserved_IsClean()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream()]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream()]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source: source, output: output, violations: violations);

        violations.Should().BeEmpty();
    }

    // ── subtitles + chapters ───────────────────────────────────────────────────

    [Fact]
    public void Subtitle_LanguageStripped_IsFlagged()
    {
        ProbedMedia source = Media(streams: [VideoStream(), SubStream(language: "spa")]);
        ProbedMedia output = Media(streams: [VideoStream(), SubStream(language: "und")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckSubtitleFidelity(source: source, output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("subtitle-language-stripped"));
    }

    [Fact]
    public void Chapters_Dropped_IsFlagged()
    {
        ProbedMedia source = Media(chapters: [new JObject { [propertyName: "id"] = 1 }]);
        ProbedMedia output = Media(chapters: []);

        List<string> violations = [];
        EncodeFidelityOracle.CheckChaptersPreserved(source: source, output: output, violations: violations);

        violations.Should().ContainSingle().Which.Should().Contain(expected: "chapters-dropped");
    }

    // ── SDR colour consistency ─────────────────────────────────────────────────

    [Fact]
    public void Sdr_WithResidualPqTransfer_IsFlagged()
    {
        // bt709 primaries but still tagged PQ — the colour re-stamp after tonemap
        // was missed.
        ProbedMedia output = Media(
            streams: [VideoStream(transfer: "smpte2084", primaries: "bt709", space: "bt709")],
            sideData: []
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckSdrColorConsistency(output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("SDR-residual-hdr-transfer"));
    }

    [Fact]
    public void Sdr_CleanBt709_IsClean()
    {
        ProbedMedia output = Media(
            streams:
            [
                VideoStream(
                    transfer: "bt709",
                    primaries: "bt709",
                    space: "bt709",
                    pixFmt: "yuv420p"
                ),
            ],
            sideData: []
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckSdrColorConsistency(output: output, violations: violations);

        violations.Should().BeEmpty();
    }

    // ── A/V start alignment ─────────────────────────────────────────────────────

    [Fact]
    public void AvSync_LargeStartDelta_IsFlagged()
    {
        JObject video = VideoStream();
        video[propertyName: "start_time"] = "0.000";
        JObject audio = AudioStream();
        audio[propertyName: "start_time"] = "0.400"; // 400ms ahead → lip-sync drift

        ProbedMedia output = Media(streams: [video, audio]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAvStartAlignment(output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("av-sync-drift"));
    }

    [Fact]
    public void AvSync_Aligned_IsClean()
    {
        JObject video = VideoStream();
        video[propertyName: "start_time"] = "0.000";
        JObject audio = AudioStream();
        audio[propertyName: "start_time"] = "0.010";

        ProbedMedia output = Media(streams: [video, audio]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAvStartAlignment(output: output, violations: violations);

        violations.Should().BeEmpty();
    }

    // ── anamorphic + rotation ───────────────────────────────────────────────────

    [Fact]
    public void Anamorphic_DarCollapsed_IsFlagged()
    {
        JObject src = VideoStream();
        src[propertyName: "display_aspect_ratio"] = "16:9";
        JObject outp = VideoStream();
        outp[propertyName: "display_aspect_ratio"] = "4:3"; // squished

        List<string> violations = [];
        EncodeFidelityOracle.CheckAnamorphicPreserved(
            source: Media(streams: [src]),
            output: Media(streams: [outp]),
            violations: violations
        );

        violations.Should().Contain(predicate: v => v.Contains("anamorphic-dar-lost"));
    }

    [Fact]
    public void Rotation_Lost_IsFlagged()
    {
        JObject srcV = VideoStream();
        srcV[propertyName: "width"] = 1080;
        srcV[propertyName: "height"] = 1920;
        ProbedMedia source = new()
        {
            Path = "s",
            Streams = [srcV],
            Format = new JObject(),
            Chapters = [],
            FirstFrameSideData =
            [
                new JObject { [propertyName: "side_data_type"] = "Display Matrix", [propertyName: "rotation"] = "90" },
            ],
        };

        // Output: same dims, no rotation matrix, dims NOT swapped → rotation lost.
        JObject outV = VideoStream();
        outV[propertyName: "width"] = 1080;
        outV[propertyName: "height"] = 1920;
        ProbedMedia output = new()
        {
            Path = "o",
            Streams = [outV],
            Format = new JObject(),
            Chapters = [],
            FirstFrameSideData = [],
        };

        List<string> violations = [];
        EncodeFidelityOracle.CheckRotationPreserved(source: source, output: output, violations: violations);

        violations.Should().Contain(predicate: v => v.Contains("rotation-lost-or-doubled"));
    }

    // ── the full suite on a clean vs corrupt output ────────────────────────────

    [Fact]
    public void Validate_CleanEncode_ReturnsNoViolations()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream(), SubStream()]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(), SubStream()]);

        EncodeFidelityOracle.Validate(source: source, output: output).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ThePunisherCorruption_CatchesTagAndAudio()
    {
        // Reproduces exactly what shipped: dvh1 without RPU + a dropped audio track.
        ProbedMedia source = Media(
            streams:
            [
                VideoStream(),
                AudioStream(language: "eng"),
                AudioStream(language: "eng", isDefault: 0),
            ]
        );
        ProbedMedia output = Media(
            streams: [VideoStream(codecTag: "dvh1"), AudioStream(language: "eng")],
            sideData: [MasteringDisplay()]
        );

        List<string> violations = EncodeFidelityOracle.Validate(source: source, output: output);

        violations.Should().Contain(predicate: v => v.Contains("DV-tag-without-RPU"));
        violations.Should().Contain(predicate: v => v.Contains("audio-tracks-dropped"));
    }
}
