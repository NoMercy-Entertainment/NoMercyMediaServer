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
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class CodecDefaultsTests
{
    [Fact]
    public void For_H264_returns_documented_defaults()
    {
        CodecDefaults.VideoDefaults defaults = CodecDefaults.For(codec: VideoCodecType.H264);
        defaults.Crf.Should().Be(expected: 22);
        defaults.Preset.Should().Be(expected: "medium");
        defaults.Profile.Should().Be(expected: CodecProfile.High);
        defaults.BitDepth.Should().Be(expected: 8);
    }

    [Fact]
    public void For_HEVC_returns_documented_defaults()
    {
        CodecDefaults.VideoDefaults defaults = CodecDefaults.For(codec: VideoCodecType.H265);
        defaults.Crf.Should().Be(expected: 20);
        defaults.Preset.Should().Be(expected: "slow");
        defaults.Profile.Should().Be(expected: CodecProfile.Main10);
        defaults.BitDepth.Should().Be(expected: 10);
    }

    [Fact]
    public void For_AAC_returns_documented_defaults()
    {
        CodecDefaults.AudioDefaults defaults = CodecDefaults.For(codec: AudioCodecType.Aac);
        defaults.BitrateKbps.Should().Be(expected: 192);
        defaults.Channels.Should().Be(expected: 2);
        defaults.SampleRateHz.Should().Be(expected: 48000);
    }

    [Fact]
    public void For_FLAC_returns_zero_bitrate_for_lossless()
    {
        CodecDefaults.AudioDefaults defaults = CodecDefaults.For(codec: AudioCodecType.Flac);
        defaults.BitrateKbps.Should().Be(expected: 0);
    }

    [Fact]
    public void For_unknown_video_codec_throws()
    {
        Action act = () => CodecDefaults.For(codec: (VideoCodecType)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(data: VideoCodecType.H264)]
    [InlineData(data: VideoCodecType.H265)]
    [InlineData(data: VideoCodecType.Av1)]
    [InlineData(data: VideoCodecType.Vp9)]
    public void Every_supported_video_codec_returns_non_null_defaults(VideoCodecType codec)
    {
        Action act = () => CodecDefaults.For(codec: codec);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(data: AudioCodecType.Aac)]
    [InlineData(data: AudioCodecType.Mp3)]
    [InlineData(data: AudioCodecType.Opus)]
    [InlineData(data: AudioCodecType.Flac)]
    [InlineData(data: AudioCodecType.Ac3)]
    [InlineData(data: AudioCodecType.Eac3)]
    [InlineData(data: AudioCodecType.TrueHd)]
    [InlineData(data: AudioCodecType.Dts)]
    [InlineData(data: AudioCodecType.Vorbis)]
    public void Every_supported_audio_codec_returns_non_null_defaults(AudioCodecType codec)
    {
        Action act = () => CodecDefaults.For(codec: codec);
        act.Should().NotThrow();
    }

    [Fact]
    public void Boundary_int_MinValue_throws_for_video()
    {
        Action act = () => CodecDefaults.For(codec: (VideoCodecType)int.MinValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Boundary_int_MaxValue_throws_for_video()
    {
        Action act = () => CodecDefaults.For(codec: (VideoCodecType)int.MaxValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Boundary_negative_throws_for_audio()
    {
        Action act = () => CodecDefaults.For(codec: (AudioCodecType)(-1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Video_defaults_table_is_stable()
    {
        CodecDefaults
            .For(codec: VideoCodecType.H264)
            .Should()
            .Be(expected: new CodecDefaults.VideoDefaults(Crf: 22, Preset: "medium", Profile: CodecProfile.High, BitDepth: 8));
        CodecDefaults
            .For(codec: VideoCodecType.H265)
            .Should()
            .Be(expected: new CodecDefaults.VideoDefaults(Crf: 20, Preset: "slow", Profile: CodecProfile.Main10, BitDepth: 10));
        CodecDefaults
            .For(codec: VideoCodecType.Av1)
            .Should()
            .Be(expected: new CodecDefaults.VideoDefaults(Crf: 30, Preset: "6", Profile: CodecProfile.Main, BitDepth: 10));
        CodecDefaults
            .For(codec: VideoCodecType.Vp9)
            .Should()
            .Be(expected: new CodecDefaults.VideoDefaults(Crf: 32, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8));
    }

    [Fact]
    public void Audio_defaults_table_is_stable()
    {
        CodecDefaults
            .For(codec: AudioCodecType.Aac)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 192, Channels: 2, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.Mp3)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 320, Channels: 2, SampleRateHz: 44100));
        CodecDefaults
            .For(codec: AudioCodecType.Opus)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 128, Channels: 2, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.Flac)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 0, Channels: 2, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.Ac3)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 384, Channels: 6, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.Eac3)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 448, Channels: 6, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.TrueHd)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 0, Channels: 6, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.Dts)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 1536, Channels: 6, SampleRateHz: 48000));
        CodecDefaults
            .For(codec: AudioCodecType.Vorbis)
            .Should()
            .Be(expected: new CodecDefaults.AudioDefaults(BitrateKbps: 192, Channels: 2, SampleRateHz: 48000));
    }
}
