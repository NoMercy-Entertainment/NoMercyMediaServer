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
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    [InlineData("vp9_qsv")]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("av1_amf")]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    [InlineData("vp9_vaapi")]
    public void IsHardware_returns_true_for_each_vendor_suffix(string encoder)
    {
        CodecRegistry.IsHardware(encoder).Should().BeTrue();
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("libvpx-vp9")]
    [InlineData("libaom-av1")]
    [InlineData("copy")]
    public void IsHardware_returns_false_for_software_encoders(string encoder)
    {
        CodecRegistry.IsHardware(encoder).Should().BeFalse();
    }

    [Theory]
    [InlineData("H264_NVENC")]
    [InlineData("HEVC_QSV")]
    [InlineData("Av1_Amf")]
    [InlineData("h264_VAAPI")]
    public void IsHardware_match_is_case_insensitive(string encoder)
    {
        CodecRegistry.IsHardware(encoder).Should().BeTrue();
    }

    [Fact]
    public void IsHardware_empty_string_returns_false()
    {
        CodecRegistry.IsHardware(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void IsHardware_arbitrary_string_returns_false()
    {
        CodecRegistry.IsHardware("some_random_name").Should().BeFalse();
    }

    // ── EnumerateVideoEncoders excludes Copy ────────────────────────────────

    [Fact]
    public void EnumerateVideoEncoders_excludes_Copy_codec()
    {
        // HardwareBenchmark consumes this — picking Copy would measure mux
        // throughput and dominate the speed index, breaking encoder selection.
        IEnumerable<(VideoCodecType CodecType, EncoderInfo Encoder)> all =
            _registry.EnumerateVideoEncoders();

        all.Should().NotContain(e => e.CodecType == VideoCodecType.Copy);
    }

    [Fact]
    public void EnumerateVideoEncoders_includes_every_other_codec_at_least_once()
    {
        IEnumerable<(VideoCodecType CodecType, EncoderInfo Encoder)> all =
            _registry.EnumerateVideoEncoders();

        IEnumerable<VideoCodecType> codecs = all.Select(e => e.CodecType).Distinct();
        codecs
            .Should()
            .Contain(VideoCodecType.H264)
            .And.Contain(VideoCodecType.H265)
            .And.Contain(VideoCodecType.Av1)
            .And.Contain(VideoCodecType.Vp9);
    }

    // ── GetAudioEncoder delegates to definitions table ──────────────────────

    [Theory]
    [InlineData(AudioCodecType.Aac, "libfdk_aac")]
    [InlineData(AudioCodecType.Flac, "flac")]
    [InlineData(AudioCodecType.Opus, "libopus")]
    [InlineData(AudioCodecType.Ac3, "ac3")]
    [InlineData(AudioCodecType.Eac3, "eac3")]
    [InlineData(AudioCodecType.Mp3, "libmp3lame")]
    [InlineData(AudioCodecType.Vorbis, "libvorbis")]
    [InlineData(AudioCodecType.TrueHd, "truehd")]
    [InlineData(AudioCodecType.Dts, "dca")]
    [InlineData(AudioCodecType.Copy, "copy")]
    public void GetAudioEncoder_returns_canonical_ffmpeg_name_per_codec(
        AudioCodecType codec,
        string expectedFfmpegName
    )
    {
        AudioEncoderInfo info = _registry.GetAudioEncoder(codec);
        info.FfmpegName.Should().Be(expectedFfmpegName);
    }

    // ── GetVideoEncoderByName behavior ──────────────────────────────────────

    [Fact]
    public void GetVideoEncoderByName_returns_encoder_for_known_name()
    {
        EncoderInfo? info = _registry.GetVideoEncoderByName("libx264");
        info.Should().NotBeNull();
        info!.FfmpegName.Should().Be("libx264");
    }

    [Fact]
    public void GetVideoEncoderByName_is_exact_match_only_case_sensitive()
    {
        // Dictionary lookup uses default StringComparer (ordinal) — uppercase
        // wouldn't match. Pinned so a future change to OrdinalIgnoreCase is
        // intentional.
        EncoderInfo? info = _registry.GetVideoEncoderByName("LIBX264");
        info.Should().BeNull();
    }

    [Fact]
    public void Constructor_builds_encoder_name_index_from_definitions()
    {
        // Smoke check — every encoder yielded by EnumerateVideoEncoders should
        // be retrievable by name through GetVideoEncoderByName.
        foreach ((VideoCodecType _, EncoderInfo encoder) in _registry.EnumerateVideoEncoders())
        {
            EncoderInfo? lookup = _registry.GetVideoEncoderByName(encoder.FfmpegName);
            lookup.Should().NotBeNull();
            lookup!.FfmpegName.Should().Be(encoder.FfmpegName);
        }
    }
}
