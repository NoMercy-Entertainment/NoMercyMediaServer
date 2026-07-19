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
            ["codec_type"] = "video",
            ["codec_name"] = codecName,
            ["codec_tag_string"] = codecTag,
            ["color_transfer"] = transfer,
            ["color_primaries"] = primaries,
            ["color_space"] = space,
            ["pix_fmt"] = pixFmt,
        };

    private static JObject AudioStream(
        string codecName = "eac3",
        string layout = "5.1(side)",
        string language = "eng",
        int isDefault = 1
    ) =>
        new()
        {
            ["codec_type"] = "audio",
            ["codec_name"] = codecName,
            ["channel_layout"] = layout,
            ["disposition"] = new JObject { ["default"] = isDefault },
            ["tags"] = new JObject { ["language"] = language },
        };

    private static JObject SubStream(string codecName = "mov_text", string language = "eng") =>
        new()
        {
            ["codec_type"] = "subtitle",
            ["codec_name"] = codecName,
            ["tags"] = new JObject { ["language"] = language },
        };

    private static JObject MasteringDisplay() =>
        new() { ["side_data_type"] = "Mastering display metadata" };

    private static JObject DoviRecord() =>
        new() { ["side_data_type"] = "DOVI configuration record", ["rpu_present_flag"] = 1 };

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
        EncodeFidelityOracle.CheckDolbyVisionTagCoherence(output, violations);

        violations.Should().ContainSingle().Which.Should().Contain("DV-tag-without-RPU");
    }

    [Fact]
    public void DvTag_WithRpu_IsClean()
    {
        ProbedMedia output = Media(
            streams: [VideoStream(codecTag: "dvh1")],
            sideData: [MasteringDisplay(), DoviRecord()]
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckDolbyVisionTagCoherence(output, violations);

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
        EncodeFidelityOracle.CheckDolbyVisionTagCoherence(output, violations);

        violations.Should().BeEmpty();
    }

    // ── HDR10 signalling ───────────────────────────────────────────────────────

    [Fact]
    public void Hdr10_MissingMasteringDisplay_IsFlagged()
    {
        ProbedMedia output = Media(streams: [VideoStream()], sideData: []); // PQ but no MDCV

        List<string> violations = [];
        EncodeFidelityOracle.CheckHdr10Signaling(output, violations);

        violations.Should().ContainSingle().Which.Should().Contain("HDR10-mastering-display");
    }

    [Fact]
    public void Hdr10_WrongPrimaries_IsFlagged()
    {
        ProbedMedia output = Media(
            streams: [VideoStream(primaries: "bt709")],
            sideData: [MasteringDisplay()]
        );

        List<string> violations = [];
        EncodeFidelityOracle.CheckHdr10Signaling(output, violations);

        violations.Should().Contain(v => v.Contains("HDR10-primaries"));
    }

    [Fact]
    public void Hdr10_Complete_IsClean()
    {
        List<string> violations = [];
        EncodeFidelityOracle.CheckHdr10Signaling(Media(), violations);
        violations.Should().BeEmpty();
    }

    // ── hvc1 vs hev1 ───────────────────────────────────────────────────────────

    [Fact]
    public void Hev1Tag_IsFlagged()
    {
        ProbedMedia output = Media(streams: [VideoStream(codecTag: "hev1")]);
        List<string> violations = [];
        EncodeFidelityOracle.CheckHevcFmp4Tag(output, violations);
        violations.Should().ContainSingle().Which.Should().Contain("HEVC-tag-hev1");
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
        EncodeFidelityOracle.CheckMasterPlaylist(master, violations);

        violations.Should().Contain(v => v.Contains("HLS-identical-bandwidth"));
    }

    [Fact]
    public void Master_DistinctBandwidth_IsClean()
    {
        string master =
            "#EXTM3U\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=11084402,RESOLUTION=3840x2160,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=PQ\nv4k.m3u8\n"
            + "#EXT-X-STREAM-INF:BANDWIDTH=4192000,RESOLUTION=1920x1080,CODECS=\"hvc1.2.4.L120.B0\",VIDEO-RANGE=SDR\nv1080.m3u8\n";

        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(master, violations);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Master_MissingCodecsAndVideoRange_IsFlagged()
    {
        string master =
            "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080\nv.m3u8\n";
        List<string> violations = [];
        EncodeFidelityOracle.CheckMasterPlaylist(master, violations);
        violations.Should().Contain(v => v.Contains("HLS-missing-codecs"));
        violations.Should().Contain(v => v.Contains("HLS-missing-video-range"));
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
        EncodeFidelityOracle.CheckAudioFidelity(source, output, violations);

        violations.Should().Contain(v => v.Contains("audio-tracks-dropped"));
    }

    [Fact]
    public void Audio_LanguageStripped_IsFlagged()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream(language: "eng")]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(language: "und")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source, output, violations);

        violations.Should().Contain(v => v.Contains("audio-language-stripped"));
    }

    [Fact]
    public void Audio_ChannelLayoutChangedOnCopy_IsFlagged()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream(layout: "5.1(side)")]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(layout: "5.1")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source, output, violations);

        violations.Should().Contain(v => v.Contains("audio-channel-layout"));
    }

    [Fact]
    public void Audio_Preserved_IsClean()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream()]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream()]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckAudioFidelity(source, output, violations);

        violations.Should().BeEmpty();
    }

    // ── subtitles + chapters ───────────────────────────────────────────────────

    [Fact]
    public void Subtitle_LanguageStripped_IsFlagged()
    {
        ProbedMedia source = Media(streams: [VideoStream(), SubStream(language: "spa")]);
        ProbedMedia output = Media(streams: [VideoStream(), SubStream(language: "und")]);

        List<string> violations = [];
        EncodeFidelityOracle.CheckSubtitleFidelity(source, output, violations);

        violations.Should().Contain(v => v.Contains("subtitle-language-stripped"));
    }

    [Fact]
    public void Chapters_Dropped_IsFlagged()
    {
        ProbedMedia source = Media(chapters: [new JObject { ["id"] = 1 }]);
        ProbedMedia output = Media(chapters: []);

        List<string> violations = [];
        EncodeFidelityOracle.CheckChaptersPreserved(source, output, violations);

        violations.Should().ContainSingle().Which.Should().Contain("chapters-dropped");
    }

    // ── the full suite on a clean vs corrupt output ────────────────────────────

    [Fact]
    public void Validate_CleanEncode_ReturnsNoViolations()
    {
        ProbedMedia source = Media(streams: [VideoStream(), AudioStream(), SubStream()]);
        ProbedMedia output = Media(streams: [VideoStream(), AudioStream(), SubStream()]);

        EncodeFidelityOracle.Validate(source, output).Should().BeEmpty();
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

        List<string> violations = EncodeFidelityOracle.Validate(source, output);

        violations.Should().Contain(v => v.Contains("DV-tag-without-RPU"));
        violations.Should().Contain(v => v.Contains("audio-tracks-dropped"));
    }
}
