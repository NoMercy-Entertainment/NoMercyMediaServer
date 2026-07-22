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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// Branch-coverage gaps for <see cref="CodecRegistry"/> beyond
/// <see cref="CodecRegistryTests"/>:
///
/// • <see cref="CodecRegistry.IsHardware"/> static — exercises each vendor
///   suffix (nvenc/qsv/amf/videotoolbox/vaapi) and the case-insensitive match.
/// • EnumerateVideoEncoders excludes Copy — pinned because including Copy
///   would let HardwareBenchmark measure ffmpeg's mux throughput and pollute
///   the speed index with a dominant top-of-list entry.
/// • GetAudioEncoder delegates to the static AudioCodecDefinitions table.
/// </summary>
public class CodecRegistryBranchTests
{
    private readonly CodecRegistry _registry = new();

    // ── IsHardware vendor-suffix table ──────────────────────────────────────

    [Theory]
    [InlineData(data: "h264_nvenc")]
    [InlineData(data: "hevc_nvenc")]
    [InlineData(data: "av1_nvenc")]
    [InlineData(data: "h264_qsv")]
    [InlineData(data: "hevc_qsv")]
    [InlineData(data: "av1_qsv")]
    [InlineData(data: "vp9_qsv")]
    [InlineData(data: "h264_amf")]
    [InlineData(data: "hevc_amf")]
    [InlineData(data: "av1_amf")]
    [InlineData(data: "h264_videotoolbox")]
    [InlineData(data: "hevc_videotoolbox")]
    [InlineData(data: "h264_vaapi")]
    [InlineData(data: "hevc_vaapi")]
    [InlineData(data: "vp9_vaapi")]
    public void IsHardware_returns_true_for_each_vendor_suffix(string encoder)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: encoder).Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "libx264")]
    [InlineData(data: "libx265")]
    [InlineData(data: "libsvtav1")]
    [InlineData(data: "libvpx-vp9")]
    [InlineData(data: "libaom-av1")]
    [InlineData(data: "copy")]
    public void IsHardware_returns_false_for_software_encoders(string encoder)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: encoder).Should().BeFalse();
    }

    [Theory]
    [InlineData(data: "H264_NVENC")]
    [InlineData(data: "HEVC_QSV")]
    [InlineData(data: "Av1_Amf")]
    [InlineData(data: "h264_VAAPI")]
    public void IsHardware_match_is_case_insensitive(string encoder)
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: encoder).Should().BeTrue();
    }

    [Fact]
    public void IsHardware_empty_string_returns_false()
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: string.Empty).Should().BeFalse();
    }

    [Fact]
    public void IsHardware_arbitrary_string_returns_false()
    {
        CodecRegistry.IsHardware(ffmpegEncoderName: "some_random_name").Should().BeFalse();
    }

    // ── EnumerateVideoEncoders excludes Copy ────────────────────────────────

    [Fact]
    public void EnumerateVideoEncoders_excludes_Copy_codec()
    {
        // HardwareBenchmark consumes this — picking Copy would measure mux
        // throughput and dominate the speed index, breaking encoder selection.
        IEnumerable<(VideoCodecType CodecType, EncoderInfo Encoder)> all =
            _registry.EnumerateVideoEncoders();

        all.Should().NotContain(predicate: e => e.CodecType == VideoCodecType.Copy);
    }

    [Fact]
    public void EnumerateVideoEncoders_includes_every_other_codec_at_least_once()
    {
        IEnumerable<(VideoCodecType CodecType, EncoderInfo Encoder)> all =
            _registry.EnumerateVideoEncoders();

        IEnumerable<VideoCodecType> codecs = all.Select(selector: e => e.CodecType).Distinct();
        codecs
            .Should()
            .Contain(expected: VideoCodecType.H264)
            .And.Contain(expected: VideoCodecType.H265)
            .And.Contain(expected: VideoCodecType.Av1)
            .And.Contain(expected: VideoCodecType.Vp9);
    }

    // ── GetAudioEncoder delegates to definitions table ──────────────────────

    [Theory]
    [InlineData(data: [AudioCodecType.Aac, "libfdk_aac"])]
    [InlineData(data: [AudioCodecType.Flac, "flac"])]
    [InlineData(data: [AudioCodecType.Opus, "libopus"])]
    [InlineData(data: [AudioCodecType.Ac3, "ac3"])]
    [InlineData(data: [AudioCodecType.Eac3, "eac3"])]
    [InlineData(data: [AudioCodecType.Mp3, "libmp3lame"])]
    [InlineData(data: [AudioCodecType.Vorbis, "libvorbis"])]
    [InlineData(data: [AudioCodecType.TrueHd, "truehd"])]
    [InlineData(data: [AudioCodecType.Dts, "dca"])]
    [InlineData(data: [AudioCodecType.Copy, "copy"])]
    public void GetAudioEncoder_returns_canonical_ffmpeg_name_per_codec(
        AudioCodecType codec,
        string expectedFfmpegName
    )
    {
        AudioEncoderInfo info = _registry.GetAudioEncoder(codecType: codec);
        info.FfmpegName.Should().Be(expected: expectedFfmpegName);
    }

    // ── GetVideoEncoderByName behavior ──────────────────────────────────────

    [Fact]
    public void GetVideoEncoderByName_returns_encoder_for_known_name()
    {
        EncoderInfo? info = _registry.GetVideoEncoderByName(ffmpegName: "libx264");
        info.Should().NotBeNull();
        info!.FfmpegName.Should().Be(expected: "libx264");
    }

    [Fact]
    public void GetVideoEncoderByName_is_exact_match_only_case_sensitive()
    {
        // Dictionary lookup uses default StringComparer (ordinal) — uppercase
        // wouldn't match. Pinned so a future change to OrdinalIgnoreCase is
        // intentional.
        EncoderInfo? info = _registry.GetVideoEncoderByName(ffmpegName: "LIBX264");
        info.Should().BeNull();
    }

    [Fact]
    public void Constructor_builds_encoder_name_index_from_definitions()
    {
        // Smoke check — every encoder yielded by EnumerateVideoEncoders should
        // be retrievable by name through GetVideoEncoderByName.
        foreach ((VideoCodecType _, EncoderInfo encoder) in _registry.EnumerateVideoEncoders())
        {
            EncoderInfo? lookup = _registry.GetVideoEncoderByName(ffmpegName: encoder.FfmpegName);
            lookup.Should().NotBeNull();
            lookup!.FfmpegName.Should().Be(expected: encoder.FfmpegName);
        }
    }
}
