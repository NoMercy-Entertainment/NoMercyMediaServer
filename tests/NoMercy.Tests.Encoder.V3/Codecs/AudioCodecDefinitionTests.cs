namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Codecs.Definitions;

public class AudioCodecDefinitionTests
{
    [Fact]
    public void Aac_HasCorrectEncoder()
    {
        AudioEncoderInfo aac = AudioCodecDefinitions.GetEncoder(AudioCodecType.Aac);
        aac.FfmpegName.Should().Be("aac");
        aac.Channels.Should().Contain(2);
        aac.Channels.Should().Contain(6);
    }

    [Fact]
    public void Flac_IsLossless()
    {
        AudioEncoderInfo flac = AudioCodecDefinitions.GetEncoder(AudioCodecType.Flac);
        flac.FfmpegName.Should().Be("flac");
        flac.IsLossless.Should().BeTrue();
    }

    [Fact]
    public void Opus_HasCorrectBitrateRange()
    {
        AudioEncoderInfo opus = AudioCodecDefinitions.GetEncoder(AudioCodecType.Opus);
        opus.FfmpegName.Should().Be("libopus");
        opus.MinBitrateKbps.Should().Be(6);
        opus.MaxBitrateKbps.Should().Be(510);
    }

    [Fact]
    public void AllAudioTypes_HaveDefinitions()
    {
        foreach (AudioCodecType codecType in Enum.GetValues<AudioCodecType>())
        {
            AudioEncoderInfo encoder = AudioCodecDefinitions.GetEncoder(codecType);
            encoder.Should().NotBeNull();
            encoder.FfmpegName.Should().NotBeNullOrEmpty();
        }
    }
}
