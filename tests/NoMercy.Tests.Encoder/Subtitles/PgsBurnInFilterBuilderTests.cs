using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// PgsBurnInFilterBuilder produces a <c>-filter_complex</c> overlay chain
/// that composites bitmap subtitle frames onto the video stream.
/// </summary>
public class PgsBurnInFilterBuilderTests
{
    private readonly PgsBurnInFilterBuilder _builder = new();

    [Fact]
    public void Build_VideoIndex0_SubtitleIndex1_ProducesExpectedFilterComplex()
    {
        PgsBurnInFilterChain chain = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 1);

        Assert.Equal("[0:v:0][0:s:1]overlay=format=auto[burned]", chain.FilterComplex);
    }

    [Fact]
    public void Build_VideoIndex0_SubtitleIndex0_ProducesExpectedFilterComplex()
    {
        PgsBurnInFilterChain chain = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);

        Assert.Equal("[0:v:0][0:s:0]overlay=format=auto[burned]", chain.FilterComplex);
    }

    [Fact]
    public void Build_AlwaysReturnsMapLabelBurned()
    {
        PgsBurnInFilterChain chain = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 3);

        Assert.Equal("[burned]", chain.MapLabel);
    }

    [Fact]
    public void Build_VideoIndex1_SubtitleIndex2_ProducesCorrectStreamSelectors()
    {
        PgsBurnInFilterChain chain = _builder.Build(videoStreamIndex: 1, subtitleStreamIndex: 2);

        Assert.Contains("[0:v:1]", chain.FilterComplex);
        Assert.Contains("[0:s:2]", chain.FilterComplex);
    }

    [Fact]
    public void Build_FilterComplexContainsOverlayFormatAuto()
    {
        PgsBurnInFilterChain chain = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);

        Assert.Contains("overlay=format=auto", chain.FilterComplex);
    }
}
