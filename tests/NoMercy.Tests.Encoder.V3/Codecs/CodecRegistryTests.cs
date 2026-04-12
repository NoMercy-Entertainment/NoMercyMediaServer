namespace NoMercy.Tests.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Codecs;

public class CodecRegistryTests
{
    private readonly CodecRegistry _registry = new();

    [Theory]
    [InlineData(VideoCodecType.H264)]
    [InlineData(VideoCodecType.H265)]
    [InlineData(VideoCodecType.Av1)]
    [InlineData(VideoCodecType.Vp9)]
    public void GetDefinition_ReturnsForAllVideoCodecs(VideoCodecType codecType)
    {
        ICodecDefinition definition = _registry.GetVideoDefinition(codecType);
        definition.Should().NotBeNull();
        definition.CodecType.Should().Be(codecType);
        definition.Encoders.Should().NotBeEmpty();
    }

    [Fact]
    public void GetVideoEncoder_ByFfmpegName_ReturnsCorrect()
    {
        EncoderInfo? nvenc = _registry.GetVideoEncoderByName("h264_nvenc");
        nvenc.Should().NotBeNull();
        nvenc!.FfmpegName.Should().Be("h264_nvenc");
    }

    [Fact]
    public void GetVideoEncoder_UnknownName_ReturnsNull()
    {
        EncoderInfo? unknown = _registry.GetVideoEncoderByName("vp9_nvenc");
        unknown.Should().BeNull();
    }

    [Fact]
    public void AllVideoEncoders_HaveUniqueNames()
    {
        List<string> allNames = [];
        foreach (VideoCodecType codecType in Enum.GetValues<VideoCodecType>())
        {
            ICodecDefinition def = _registry.GetVideoDefinition(codecType);
            allNames.AddRange(def.Encoders.Select(e => e.FfmpegName));
        }
        allNames.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TotalVideoEncoderCount_Is22()
    {
        int total = 0;
        foreach (VideoCodecType codecType in Enum.GetValues<VideoCodecType>())
        {
            total += _registry.GetVideoDefinition(codecType).Encoders.Length;
        }
        total.Should().Be(22);
    }
}
