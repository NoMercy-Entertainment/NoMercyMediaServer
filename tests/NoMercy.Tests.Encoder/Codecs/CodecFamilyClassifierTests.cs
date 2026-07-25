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
/// Single-source-of-truth tests for encoder-name → codec family classification.
/// Covers every FFmpeg encoder name the project knows about so future encoder
/// additions (AMD AV1 variants, AppleSilicon RT codecs) have to update the
/// classifier instead of slipping past three separate Contains-chains.
/// </summary>
public class CodecFamilyClassifierTests
{
    // ── Video classification ────────────────────────────────────────────────

    [Theory]
    [InlineData(["libx264", VideoCodecType.H264])]
    [InlineData(["h264_nvenc", VideoCodecType.H264])]
    [InlineData(["h264_amf", VideoCodecType.H264])]
    [InlineData(["h264_qsv", VideoCodecType.H264])]
    [InlineData(["h264_vaapi", VideoCodecType.H264])]
    [InlineData(["h264_videotoolbox", VideoCodecType.H264])]
    [InlineData(["x264", VideoCodecType.H264])]
    public void ClassifyVideo_H264Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["libx265", VideoCodecType.H265])]
    [InlineData(["hevc_nvenc", VideoCodecType.H265])]
    [InlineData(["hevc_amf", VideoCodecType.H265])]
    [InlineData(["hevc_qsv", VideoCodecType.H265])]
    [InlineData(["hevc_vaapi", VideoCodecType.H265])]
    [InlineData(["hevc_videotoolbox", VideoCodecType.H265])]
    public void ClassifyVideo_H265Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["libsvtav1", VideoCodecType.Av1])]
    [InlineData(["libaom-av1", VideoCodecType.Av1])]
    [InlineData(["av1_nvenc", VideoCodecType.Av1])]
    [InlineData(["av1_amf", VideoCodecType.Av1])]
    [InlineData(["av1_qsv", VideoCodecType.Av1])]
    [InlineData(["av1_vaapi", VideoCodecType.Av1])]
    public void ClassifyVideo_Av1Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["libvpx-vp9", VideoCodecType.Vp9])]
    [InlineData(["vp9_qsv", VideoCodecType.Vp9])]
    [InlineData(["vp9_vaapi", VideoCodecType.Vp9])]
    public void ClassifyVideo_Vp9Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("passthrough")]
    [InlineData("v_passthrough")]
    public void ClassifyVideo_CopyVariants(string name)
    {
        CodecFamilyClassifier.ClassifyVideo(name).Should().Be(VideoCodecType.Copy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonexistent_codec")]
    public void ClassifyVideo_UnknownReturnsNull(string? name)
    {
        CodecFamilyClassifier.ClassifyVideo(name!).Should().BeNull();
    }

    [Fact]
    public void ClassifyVideo_CaseInsensitive()
    {
        CodecFamilyClassifier.ClassifyVideo("LIBX264").Should().Be(VideoCodecType.H264);
        CodecFamilyClassifier.ClassifyVideo("Hevc_Nvenc").Should().Be(VideoCodecType.H265);
        CodecFamilyClassifier.ClassifyVideo("AV1_NVENC").Should().Be(VideoCodecType.Av1);
    }

    // ── Audio classification ────────────────────────────────────────────────

    [Theory]
    [InlineData(["aac", AudioCodecType.Aac])]
    [InlineData(["libfdk_aac", AudioCodecType.Aac])]
    [InlineData(["aac_at", AudioCodecType.Aac])]
    public void ClassifyAudio_AacVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["libopus", AudioCodecType.Opus])]
    [InlineData(["opus", AudioCodecType.Opus])]
    public void ClassifyAudio_OpusVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["flac", AudioCodecType.Flac])]
    [InlineData(["libflac", AudioCodecType.Flac])]
    public void ClassifyAudio_FlacVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["ac3", AudioCodecType.Ac3])]
    [InlineData(["eac3", AudioCodecType.Eac3])]
    [InlineData(["e-ac3", AudioCodecType.Eac3])]
    public void ClassifyAudio_DolbyVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["dolby_e", AudioCodecType.Ac3])]
    [InlineData(["Dolby", AudioCodecType.Ac3])]
    public void ClassifyAudio_DolbyKeywordMapsToAc3(string name, AudioCodecType expected)
    {
        // "dolby" keyword is a soft alias to AC-3 (Dolby Digital) — keeps legacy
        // naming working when sources tag streams with the marketing name.
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["libmp3lame", AudioCodecType.Mp3])]
    [InlineData(["mp3", AudioCodecType.Mp3])]
    public void ClassifyAudio_Mp3Variants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(["libvorbis", AudioCodecType.Vorbis])]
    [InlineData(["vorbis", AudioCodecType.Vorbis])]
    public void ClassifyAudio_VorbisVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("truehd")]
    [InlineData("TRUEHD")]
    public void ClassifyAudio_TrueHd(string name)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(AudioCodecType.TrueHd);
    }

    [Theory]
    [InlineData("dts")]
    [InlineData("dca")]
    [InlineData("DTS-HD")]
    public void ClassifyAudio_Dts(string name)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(AudioCodecType.Dts);
    }

    [Theory]
    [InlineData(["copy", AudioCodecType.Copy])]
    [InlineData(["passthrough", AudioCodecType.Copy])]
    public void ClassifyAudio_Copy(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonexistent")]
    public void ClassifyAudio_UnknownReturnsNull(string? name)
    {
        CodecFamilyClassifier.ClassifyAudio(name!).Should().BeNull();
    }

    // ── Family token (segment/folder naming) ────────────────────────────────

    [Theory]
    [InlineData(["libx264", "avc"])]
    [InlineData(["h264_nvenc", "avc"])]
    [InlineData(["hevc_nvenc", "hevc"])]
    [InlineData(["libx265", "hevc"])]
    [InlineData(["av1_nvenc", "av1"])]
    [InlineData(["libsvtav1", "av1"])]
    [InlineData(["libvpx-vp9", "vp9"])]
    [InlineData(["vp9_qsv", "vp9"])]
    public void FamilyToken_KnownCodecsMapToShortNames(string encoderName, string expected)
    {
        CodecFamilyClassifier.FamilyToken(encoderName).Should().Be(expected);
    }

    [Fact]
    public void FamilyToken_UnknownEncoderFallsBackToLowercased()
    {
        // Unknown encoder names get the lowercased original — preserves
        // disambiguation for codecs the classifier hasn't been taught yet.
        CodecFamilyClassifier.FamilyToken("ProResHQ").Should().Be("proreshq");
    }
}
