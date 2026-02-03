using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;

namespace NoMercy.Tests.EncoderV2.Codecs;

public class AudioCodecTests
{
    #region AacCodec Tests

    [Fact]
    public void AacCodec_DefaultSettings_BuildsCorrectArguments()
    {
        AacCodec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("aac", args);
    }

    [Fact]
    public void AacCodec_WithBitrate_IncludesBitrateInArguments()
    {
        AacCodec codec = new() { Bitrate = 192 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-b:a", args);
        Assert.Contains("192k", args);
    }

    [Fact]
    public void AacCodec_WithChannels_IncludesChannelsInArguments()
    {
        AacCodec codec = new() { Channels = 2 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-ac", args);
        Assert.Contains("2", args);
    }

    [Fact]
    public void AacCodec_WithSampleRate_IncludesSampleRateInArguments()
    {
        AacCodec codec = new() { SampleRate = 48000 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-ar", args);
        Assert.Contains("48000", args);
    }

    [Fact]
    public void AacCodec_WithVbr_IncludesQualityInArguments()
    {
        AacCodec codec = new() { Vbr = 4 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-q:a", args);
        Assert.Contains("4", args);
    }

    [Fact]
    public void AacCodec_WithProfile_IncludesProfileInArguments()
    {
        AacCodec codec = new() { Profile = "aac_he" };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-profile:a", args);
        Assert.Contains("aac_he", args);
    }

    [Fact]
    public void AacCodec_Validation_InvalidProfile_ReturnsError()
    {
        AacCodec codec = new() { Profile = "invalid_profile" };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AacCodec_Validation_InvalidVbr_ReturnsError()
    {
        AacCodec codec = new() { Vbr = 10 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AacCodec_Clone_CreatesIndependentCopy()
    {
        AacCodec original = new()
        {
            Bitrate = 192,
            Channels = 2,
            SampleRate = 48000
        };

        IAudioCodec clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Bitrate, clone.Bitrate);
    }

    #endregion

    #region OpusCodec Tests

    [Fact]
    public void OpusCodec_DefaultSettings_BuildsCorrectArguments()
    {
        OpusCodec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("libopus", args);
    }

    [Fact]
    public void OpusCodec_WithApplication_IncludesApplicationInArguments()
    {
        OpusCodec codec = new() { Application = "audio" };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-application", args);
        Assert.Contains("audio", args);
    }

    [Fact]
    public void OpusCodec_WithCompressionLevel_IncludesLevelInArguments()
    {
        OpusCodec codec = new() { CompressionLevel = 10 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-compression_level", args);
        Assert.Contains("10", args);
    }

    [Fact]
    public void OpusCodec_Validation_InvalidApplication_ReturnsError()
    {
        OpusCodec codec = new() { Application = "invalid" };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void OpusCodec_Validation_InvalidCompressionLevel_ReturnsError()
    {
        OpusCodec codec = new() { CompressionLevel = 15 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void OpusCodec_AvailableSampleRates_ContainsOpusRates()
    {
        OpusCodec codec = new();

        Assert.Contains(48000, codec.AvailableSampleRates);
        Assert.Contains(24000, codec.AvailableSampleRates);
    }

    #endregion

    #region Ac3Codec Tests

    [Fact]
    public void Ac3Codec_DefaultSettings_BuildsCorrectArguments()
    {
        Ac3Codec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("ac3", args);
    }

    [Fact]
    public void Ac3Codec_AvailableChannelLayouts_SupportsSurround()
    {
        Ac3Codec codec = new();

        Assert.Contains("5.1", codec.AvailableChannelLayouts);
    }

    [Fact]
    public void Eac3Codec_DefaultSettings_BuildsCorrectArguments()
    {
        Eac3Codec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("eac3", args);
    }

    [Fact]
    public void Eac3Codec_AvailableChannelLayouts_Supports71()
    {
        Eac3Codec codec = new();

        Assert.Contains("7.1", codec.AvailableChannelLayouts);
    }

    #endregion

    #region FlacCodec Tests

    [Fact]
    public void FlacCodec_DefaultSettings_BuildsCorrectArguments()
    {
        FlacCodec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("flac", args);
    }

    [Fact]
    public void FlacCodec_WithCompressionLevel_IncludesLevelInArguments()
    {
        FlacCodec codec = new() { CompressionLevel = 8 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-compression_level", args);
        Assert.Contains("8", args);
    }

    [Fact]
    public void FlacCodec_Validation_BitrateWarning()
    {
        FlacCodec codec = new() { Bitrate = 320 };

        ValidationResult result = codec.Validate();

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("lossless"));
    }

    [Fact]
    public void FlacCodec_Validation_InvalidCompressionLevel_ReturnsError()
    {
        FlacCodec codec = new() { CompressionLevel = 15 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    #endregion

    #region Mp3Codec Tests

    [Fact]
    public void Mp3Codec_DefaultSettings_BuildsCorrectArguments()
    {
        Mp3Codec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("libmp3lame", args);
    }

    [Fact]
    public void Mp3Codec_WithVbrQuality_IncludesQualityInArguments()
    {
        Mp3Codec codec = new() { VbrQuality = 2 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-q:a", args);
        Assert.Contains("2", args);
    }

    [Fact]
    public void Mp3Codec_Validation_TooManyChannels_ReturnsError()
    {
        Mp3Codec codec = new() { Channels = 6 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("stereo"));
    }

    [Fact]
    public void Mp3Codec_AvailableChannelLayouts_OnlyMonoAndStereo()
    {
        Mp3Codec codec = new();

        Assert.Equal(2, codec.AvailableChannelLayouts.Count);
        Assert.Contains("mono", codec.AvailableChannelLayouts);
        Assert.Contains("stereo", codec.AvailableChannelLayouts);
    }

    #endregion

    #region VorbisCodec Tests

    [Fact]
    public void VorbisCodec_DefaultSettings_BuildsCorrectArguments()
    {
        VorbisCodec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-c:a", args);
        Assert.Contains("libvorbis", args);
    }

    [Fact]
    public void VorbisCodec_WithVbrQuality_IncludesQualityInArguments()
    {
        VorbisCodec codec = new() { VbrQuality = 6.0 };

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Contains("-q:a", args);
    }

    [Fact]
    public void VorbisCodec_Validation_InvalidVbrQuality_ReturnsError()
    {
        VorbisCodec codec = new() { VbrQuality = 15 };

        ValidationResult result = codec.Validate();

        Assert.False(result.IsValid);
    }

    #endregion

    #region AudioCopyCodec Tests

    [Fact]
    public void AudioCopyCodec_BuildsSimpleCopyArguments()
    {
        AudioCopyCodec codec = new();

        IReadOnlyList<string> args = codec.BuildArguments();

        Assert.Equal(2, args.Count);
        Assert.Equal("-c:a", args[0]);
        Assert.Equal("copy", args[1]);
    }

    #endregion
}
