using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class CodecDefaultsTests
{
    [Fact]
    public void For_H264_returns_documented_defaults()
    {
        CodecDefaults.VideoDefaults defaults = CodecDefaults.For(VideoCodecType.H264);
        defaults.Crf.Should().Be(22);
        defaults.Preset.Should().Be("medium");
        defaults.Profile.Should().Be(CodecProfile.High);
        defaults.BitDepth.Should().Be(8);
    }

    [Fact]
    public void For_HEVC_returns_documented_defaults()
    {
        CodecDefaults.VideoDefaults defaults = CodecDefaults.For(VideoCodecType.H265);
        defaults.Crf.Should().Be(20);
        defaults.Preset.Should().Be("slow");
        defaults.Profile.Should().Be(CodecProfile.Main10);
        defaults.BitDepth.Should().Be(10);
    }

    [Fact]
    public void For_AAC_returns_documented_defaults()
    {
        CodecDefaults.AudioDefaults defaults = CodecDefaults.For(AudioCodecType.Aac);
        defaults.BitrateKbps.Should().Be(192);
        defaults.Channels.Should().Be(2);
        defaults.SampleRateHz.Should().Be(48000);
    }

    [Fact]
    public void For_FLAC_returns_zero_bitrate_for_lossless()
    {
        CodecDefaults.AudioDefaults defaults = CodecDefaults.For(AudioCodecType.Flac);
        defaults.BitrateKbps.Should().Be(0);
    }

    [Fact]
    public void For_unknown_video_codec_throws()
    {
        Action act = () => CodecDefaults.For((VideoCodecType)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(VideoCodecType.H264)]
    [InlineData(VideoCodecType.H265)]
    [InlineData(VideoCodecType.Av1)]
    [InlineData(VideoCodecType.Vp9)]
    public void Every_supported_video_codec_returns_non_null_defaults(VideoCodecType codec)
    {
        Action act = () => CodecDefaults.For(codec);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(AudioCodecType.Aac)]
    [InlineData(AudioCodecType.Mp3)]
    [InlineData(AudioCodecType.Opus)]
    [InlineData(AudioCodecType.Flac)]
    [InlineData(AudioCodecType.Ac3)]
    [InlineData(AudioCodecType.Eac3)]
    [InlineData(AudioCodecType.TrueHd)]
    [InlineData(AudioCodecType.Dts)]
    [InlineData(AudioCodecType.Vorbis)]
    public void Every_supported_audio_codec_returns_non_null_defaults(AudioCodecType codec)
    {
        Action act = () => CodecDefaults.For(codec);
        act.Should().NotThrow();
    }

    [Fact]
    public void Boundary_int_MinValue_throws_for_video()
    {
        Action act = () => CodecDefaults.For((VideoCodecType)int.MinValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Boundary_int_MaxValue_throws_for_video()
    {
        Action act = () => CodecDefaults.For((VideoCodecType)int.MaxValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Boundary_negative_throws_for_audio()
    {
        Action act = () => CodecDefaults.For((AudioCodecType)(-1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Video_defaults_table_is_stable()
    {
        CodecDefaults
            .For(VideoCodecType.H264)
            .Should()
            .Be(new CodecDefaults.VideoDefaults(22, "medium", CodecProfile.High, 8));
        CodecDefaults
            .For(VideoCodecType.H265)
            .Should()
            .Be(new CodecDefaults.VideoDefaults(20, "slow", CodecProfile.Main10, 10));
        CodecDefaults
            .For(VideoCodecType.Av1)
            .Should()
            .Be(new CodecDefaults.VideoDefaults(30, "6", CodecProfile.Main, 10));
        CodecDefaults
            .For(VideoCodecType.Vp9)
            .Should()
            .Be(new CodecDefaults.VideoDefaults(32, "good", CodecProfile.Main, 8));
    }

    [Fact]
    public void Audio_defaults_table_is_stable()
    {
        CodecDefaults
            .For(AudioCodecType.Aac)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(192, 2, 48000));
        CodecDefaults
            .For(AudioCodecType.Mp3)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(320, 2, 44100));
        CodecDefaults
            .For(AudioCodecType.Opus)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(128, 2, 48000));
        CodecDefaults
            .For(AudioCodecType.Flac)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(0, 2, 48000));
        CodecDefaults
            .For(AudioCodecType.Ac3)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(384, 6, 48000));
        CodecDefaults
            .For(AudioCodecType.Eac3)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(448, 6, 48000));
        CodecDefaults
            .For(AudioCodecType.TrueHd)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(0, 6, 48000));
        CodecDefaults
            .For(AudioCodecType.Dts)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(1536, 6, 48000));
        CodecDefaults
            .For(AudioCodecType.Vorbis)
            .Should()
            .Be(new CodecDefaults.AudioDefaults(192, 2, 48000));
    }
}
