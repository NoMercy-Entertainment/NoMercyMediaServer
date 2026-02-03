using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Video;

namespace NoMercy.Tests.EncoderV2.Codecs;

public class VideoCodecTests
{
    #region H264Codec Tests

    [Fact]
    public void H264Codec_DefaultSettings_BuildsCorrectArguments()
    {
        H264Codec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:v", args);
        Assert.Contains("libx264", args);
    }

    [Fact]
    public void H264Codec_WithPreset_IncludesPresetInArguments()
    {
        H264Codec codec = new() { Preset = "medium" };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-preset", args);
        Assert.Contains("medium", args);
    }

    [Fact]
    public void H264Codec_WithCrf_IncludesCrfInArguments()
    {
        H264Codec codec = new() { Crf = 23 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-crf", args);
        Assert.Contains("23", args);
    }

    [Fact]
    public void H264Codec_WithProfile_IncludesProfileInArguments()
    {
        H264Codec codec = new() { Profile = "high" };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-profile:v", args);
        Assert.Contains("high", args);
    }

    [Fact]
    public void H264Codec_WithBitrate_IncludesBitrateInArguments()
    {
        H264Codec codec = new() { Bitrate = 5000 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-b:v", args);
        Assert.Contains("5000k", args);
    }

    [Fact]
    public void H264Codec_WithPixelFormat_IncludesPixFmtInArguments()
    {
        H264Codec codec = new() { PixelFormat = "yuv420p" };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-pix_fmt", args);
        Assert.Contains("yuv420p", args);
    }

    [Fact]
    public void H264Codec_WithKeyframeInterval_IncludesGopInArguments()
    {
        H264Codec codec = new() { KeyframeInterval = 48 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-g", args);
        Assert.Contains("48", args);
    }

    [Fact]
    public void H264Codec_Validation_ValidSettings_ReturnsSuccess()
    {
        H264Codec codec = new()
        {
            Preset = "medium",
            Profile = "high",
            Crf = 23
        };

        ValidationResult result = codec.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void H264Codec_Validation_InvalidPreset_ReturnsError()
    {
        H264Codec codec = new() { Preset = "invalid_preset" };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("preset"));
    }

    [Fact]
    public void H264Codec_Validation_CrfOutOfRange_ReturnsError()
    {
        H264Codec codec = new() { Crf = 100 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CRF"));
    }

    [Fact]
    public void H264Codec_Clone_CreatesIndependentCopy()
    {
        H264Codec original = new()
        {
            Preset = "fast",
            Profile = "main",
            Crf = 20
        };

        IVideoCodec clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Preset, clone.Preset);
        Assert.Equal(original.Profile, clone.Profile);
        Assert.Equal(original.Crf, clone.Crf);
    }

    [Fact]
    public void H264Codec_AvailablePresets_ContainsCommonPresets()
    {
        H264Codec codec = new();

        Assert.Contains("ultrafast", codec.AvailablePresets);
        Assert.Contains("medium", codec.AvailablePresets);
        Assert.Contains("slow", codec.AvailablePresets);
        Assert.Contains("veryslow", codec.AvailablePresets);
    }

    [Fact]
    public void H264Codec_SupportsBFrames_ReturnsTrue()
    {
        H264Codec codec = new();

        Assert.True(codec.SupportsBFrames);
    }

    #endregion

    #region H265Codec Tests

    [Fact]
    public void H265Codec_DefaultSettings_BuildsCorrectArguments()
    {
        H265Codec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:v", args);
        Assert.Contains("libx265", args);
        // H.265 should include hvc1 tag for web playback
        Assert.Contains("-tag:v", args);
        Assert.Contains("hvc1", args);
    }

    [Fact]
    public void H265Codec_WithHdrMetadata_IncludesX265Params()
    {
        H265Codec codec = new() { CopyHdrMetadata = true };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-x265-params", args);
    }

    [Fact]
    public void H265Codec_Main10Profile_ValidForHdr()
    {
        H265Codec codec = new() { Profile = "main10" };

        ValidationResult result = codec.Validate();

        Assert.True(result.IsValid);
    }

    #endregion

    #region AV1Codec Tests

    [Fact]
    public void Av1Codec_DefaultSettings_BuildsCorrectArguments()
    {
        Av1Codec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:v", args);
        Assert.Contains("libaom-av1", args);
    }

    [Fact]
    public void Av1Codec_CrfRange_ExtendedTo63()
    {
        Av1Codec codec = new();

        Assert.Equal(0, codec.CrfRange.Min);
        Assert.Equal(63, codec.CrfRange.Max);
    }

    [Fact]
    public void Av1Codec_WithRowMt_IncludesRowMtInArguments()
    {
        Av1Codec codec = new() { RowMt = true };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-row-mt", args);
        Assert.Contains("1", args);
    }

    [Fact]
    public void Av1SvtCodec_WithFilmGrain_IncludesInParams()
    {
        Av1SvtCodec codec = new() { FilmGrain = 10 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-svtav1-params", args);
    }

    #endregion

    #region VP9Codec Tests

    [Fact]
    public void Vp9Codec_WithCrf_IncludesBvZero()
    {
        Vp9Codec codec = new() { Crf = 30 };

        IReadOnlyList<string> args = codec.BuildArguments();

        // VP9 needs -b:v 0 for CRF mode
        Assert.Contains("-b:v", args);
        Assert.Contains("0", args);
    }

    [Fact]
    public void Vp9Codec_WithSpeed_IncludesSpeedInArguments()
    {
        Vp9Codec codec = new() { Speed = 2 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-speed", args);
        Assert.Contains("2", args);
    }

    [Fact]
    public void Vp9Codec_Validation_InvalidSpeed_ReturnsError()
    {
        Vp9Codec codec = new() { Speed = 10 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    #endregion

    #region Hardware Codec Tests

    [Fact]
    public void H264NvencCodec_RequiresHardwareAcceleration()
    {
        H264NvencCodec codec = new();

        Assert.True(codec.RequiresHardwareAcceleration);
        Assert.Equal(HardwareAcceleration.Nvenc, codec.HardwareAccelerationType);
    }

    [Fact]
    public void H264NvencCodec_WithCq_UsesCqInsteadOfCrf()
    {
        H264NvencCodec codec = new() { Cq = 22 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-cq", args);
        Assert.Contains("22", args);
    }

    [Fact]
    public void H265NvencCodec_BuildsCorrectArguments()
    {
        H265NvencCodec codec = new() { Preset = "p5" };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:v", args);
        Assert.Contains("hevc_nvenc", args);
        Assert.Contains("-preset", args);
    }

    [Fact]
    public void H264QsvCodec_RequiresQsvAcceleration()
    {
        H264QsvCodec codec = new();

        Assert.True(codec.RequiresHardwareAcceleration);
        Assert.Equal(HardwareAcceleration.Qsv, codec.HardwareAccelerationType);
    }

    #endregion

    #region VideoCopyCodec Tests

    [Fact]
    public void VideoCopyCodec_BuildsSimpleCopyArguments()
    {
        VideoCopyCodec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Equal(2, args.Count);
        Assert.Equal("-c:v", args[0]);
        Assert.Equal("copy", args[1]);
    }

    [Fact]
    public void VideoCopyCodec_DoesNotRequireHardwareAcceleration()
    {
        VideoCopyCodec codec = new();

        Assert.False(codec.RequiresHardwareAcceleration);
    }

    #endregion
}
