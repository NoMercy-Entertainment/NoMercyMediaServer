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
    [InlineData(data: ["libx264", VideoCodecType.H264])]
    [InlineData(data: ["h264_nvenc", VideoCodecType.H264])]
    [InlineData(data: ["h264_amf", VideoCodecType.H264])]
    [InlineData(data: ["h264_qsv", VideoCodecType.H264])]
    [InlineData(data: ["h264_vaapi", VideoCodecType.H264])]
    [InlineData(data: ["h264_videotoolbox", VideoCodecType.H264])]
    [InlineData(data: ["x264", VideoCodecType.H264])]
    public void ClassifyVideo_H264Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["libx265", VideoCodecType.H265])]
    [InlineData(data: ["hevc_nvenc", VideoCodecType.H265])]
    [InlineData(data: ["hevc_amf", VideoCodecType.H265])]
    [InlineData(data: ["hevc_qsv", VideoCodecType.H265])]
    [InlineData(data: ["hevc_vaapi", VideoCodecType.H265])]
    [InlineData(data: ["hevc_videotoolbox", VideoCodecType.H265])]
    public void ClassifyVideo_H265Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["libsvtav1", VideoCodecType.Av1])]
    [InlineData(data: ["libaom-av1", VideoCodecType.Av1])]
    [InlineData(data: ["av1_nvenc", VideoCodecType.Av1])]
    [InlineData(data: ["av1_amf", VideoCodecType.Av1])]
    [InlineData(data: ["av1_qsv", VideoCodecType.Av1])]
    [InlineData(data: ["av1_vaapi", VideoCodecType.Av1])]
    public void ClassifyVideo_Av1Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["libvpx-vp9", VideoCodecType.Vp9])]
    [InlineData(data: ["vp9_qsv", VideoCodecType.Vp9])]
    [InlineData(data: ["vp9_vaapi", VideoCodecType.Vp9])]
    public void ClassifyVideo_Vp9Encoders(string name, VideoCodecType expected)
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "copy")]
    [InlineData(data: "passthrough")]
    [InlineData(data: "v_passthrough")]
    public void ClassifyVideo_CopyVariants(string name)
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: name).Should().Be(expected: VideoCodecType.Copy);
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    [InlineData(data: "nonexistent_codec")]
    public void ClassifyVideo_UnknownReturnsNull(string? name)
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: name!).Should().BeNull();
    }

    [Fact]
    public void ClassifyVideo_CaseInsensitive()
    {
        CodecFamilyClassifier.ClassifyVideo(encoderName: "LIBX264").Should().Be(expected: VideoCodecType.H264);
        CodecFamilyClassifier.ClassifyVideo(encoderName: "Hevc_Nvenc").Should().Be(expected: VideoCodecType.H265);
        CodecFamilyClassifier.ClassifyVideo(encoderName: "AV1_NVENC").Should().Be(expected: VideoCodecType.Av1);
    }

    // ── Audio classification ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["aac", AudioCodecType.Aac])]
    [InlineData(data: ["libfdk_aac", AudioCodecType.Aac])]
    [InlineData(data: ["aac_at", AudioCodecType.Aac])]
    public void ClassifyAudio_AacVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["libopus", AudioCodecType.Opus])]
    [InlineData(data: ["opus", AudioCodecType.Opus])]
    public void ClassifyAudio_OpusVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["flac", AudioCodecType.Flac])]
    [InlineData(data: ["libflac", AudioCodecType.Flac])]
    public void ClassifyAudio_FlacVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["ac3", AudioCodecType.Ac3])]
    [InlineData(data: ["eac3", AudioCodecType.Eac3])]
    [InlineData(data: ["e-ac3", AudioCodecType.Eac3])]
    public void ClassifyAudio_DolbyVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["dolby_e", AudioCodecType.Ac3])]
    [InlineData(data: ["Dolby", AudioCodecType.Ac3])]
    public void ClassifyAudio_DolbyKeywordMapsToAc3(string name, AudioCodecType expected)
    {
        // "dolby" keyword is a soft alias to AC-3 (Dolby Digital) — keeps legacy
        // naming working when sources tag streams with the marketing name.
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["libmp3lame", AudioCodecType.Mp3])]
    [InlineData(data: ["mp3", AudioCodecType.Mp3])]
    public void ClassifyAudio_Mp3Variants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["libvorbis", AudioCodecType.Vorbis])]
    [InlineData(data: ["vorbis", AudioCodecType.Vorbis])]
    public void ClassifyAudio_VorbisVariants(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "truehd")]
    [InlineData(data: "TRUEHD")]
    public void ClassifyAudio_TrueHd(string name)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: AudioCodecType.TrueHd);
    }

    [Theory]
    [InlineData(data: "dts")]
    [InlineData(data: "dca")]
    [InlineData(data: "DTS-HD")]
    public void ClassifyAudio_Dts(string name)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: AudioCodecType.Dts);
    }

    [Theory]
    [InlineData(data: ["copy", AudioCodecType.Copy])]
    [InlineData(data: ["passthrough", AudioCodecType.Copy])]
    public void ClassifyAudio_Copy(string name, AudioCodecType expected)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "nonexistent")]
    public void ClassifyAudio_UnknownReturnsNull(string? name)
    {
        CodecFamilyClassifier.ClassifyAudio(encoderName: name!).Should().BeNull();
    }

    // ── Family token (segment/folder naming) ────────────────────────────────

    [Theory]
    [InlineData(data: ["libx264", "avc"])]
    [InlineData(data: ["h264_nvenc", "avc"])]
    [InlineData(data: ["hevc_nvenc", "hevc"])]
    [InlineData(data: ["libx265", "hevc"])]
    [InlineData(data: ["av1_nvenc", "av1"])]
    [InlineData(data: ["libsvtav1", "av1"])]
    [InlineData(data: ["libvpx-vp9", "vp9"])]
    [InlineData(data: ["vp9_qsv", "vp9"])]
    public void FamilyToken_KnownCodecsMapToShortNames(string encoderName, string expected)
    {
        CodecFamilyClassifier.FamilyToken(encoderName: encoderName).Should().Be(expected: expected);
    }

    [Fact]
    public void FamilyToken_UnknownEncoderFallsBackToLowercased()
    {
        // Unknown encoder names get the lowercased original — preserves
        // disambiguation for codecs the classifier hasn't been taught yet.
        CodecFamilyClassifier.FamilyToken(encoderName: "ProResHQ").Should().Be(expected: "proreshq");
    }
}
