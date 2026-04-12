namespace NoMercy.Tests.Encoder.V3.Commands;

using NoMercy.Encoder.V3.Commands;

public class FilterGraphBuilderTests
{
    [Fact]
    public void SingleScale_ProducesCorrectOutput()
    {
        string result = new FilterGraphBuilder().AddScale("0:v", 1920, 1080, "v0").Build();

        result.Should().Be("[0:v]scale=1920:1080[v0]");
    }

    [Fact]
    public void SplitAndMultiScale_ProducesCorrectOutput()
    {
        string result = new FilterGraphBuilder()
            .AddSplit("0:v", ["a", "b", "c"])
            .AddScale("a", 3840, 2160, "v0")
            .AddScale("b", 1920, 1080, "v1")
            .AddScale("c", 1280, 720, "v2")
            .Build();

        result
            .Should()
            .Be(
                "[0:v]split=3[a][b][c];[a]scale=3840:2160[v0];[b]scale=1920:1080[v1];[c]scale=1280:720[v2]"
            );
    }

    [Fact]
    public void TonemapThenSplitThenScale_ProducesChain()
    {
        string result = new FilterGraphBuilder()
            .AddTonemap("0:v", "hable", "sdr")
            .AddSplit("sdr", ["a", "b"])
            .AddScale("a", 1920, 1080, "v0")
            .AddScale("b", 1280, 720, "v1")
            .Build();

        result.Should().StartWith("[0:v]zscale=t=linear");
        result.Should().Contain("[sdr]");
        result.Should().Contain("[sdr]split=2[a][b]");
        result.Should().Contain("[a]scale=1920:1080[v0]");
        result.Should().Contain("[b]scale=1280:720[v1]");
    }

    [Fact]
    public void LibplaceboTonemap_ProducesCorrectFilter()
    {
        string result = new FilterGraphBuilder()
            .AddLibplaceboTonemap("0:v", "hable", "sdr")
            .Build();

        result.Should().Contain("libplacebo=tonemapping=hable");
        result.Should().Contain("color_primaries=bt709");
    }

    [Fact]
    public void EmptyBuilder_ReturnsEmptyString()
    {
        string result = new FilterGraphBuilder().Build();
        result.Should().BeEmpty();
    }

    [Fact]
    public void HasFilters_FalseWhenEmpty()
    {
        FilterGraphBuilder builder = new();
        builder.HasFilters.Should().BeFalse();
    }

    [Fact]
    public void HasFilters_TrueAfterAdd()
    {
        FilterGraphBuilder builder = new();
        builder.AddScale("0:v", 1920, 1080, "v0");
        builder.HasFilters.Should().BeTrue();
    }

    [Fact]
    public void Deinterlace_ProducesYadif()
    {
        string result = new FilterGraphBuilder().AddDeinterlace("0:v", "deint").Build();

        result.Should().Be("[0:v]yadif[deint]");
    }

    [Fact]
    public void Crop_ProducesCorrectParams()
    {
        string result = new FilterGraphBuilder()
            .AddCrop("0:v", 1920, 800, 0, 140, "cropped")
            .Build();

        result.Should().Be("[0:v]crop=1920:800:0:140[cropped]");
    }

    [Fact]
    public void ScaleWidth_UsesMinusTwoForHeight()
    {
        string result = new FilterGraphBuilder().AddScaleWidth("0:v", 1280, "v0").Build();

        result.Should().Be("[0:v]scale=1280:-2[v0]");
    }

    [Fact]
    public void ComplexChain_4kHdrToMultiSdr()
    {
        // Simulate: 4K HDR → tonemap → split 3 → scale to 4K/1080p/720p
        string result = new FilterGraphBuilder()
            .AddTonemap("0:v", "hable", "sdr")
            .AddSplit("sdr", ["a", "b", "c"])
            .AddScale("a", 3840, 2160, "v0")
            .AddScale("b", 1920, 1080, "v1")
            .AddScale("c", 1280, 720, "v2")
            .Build();

        // Should have 5 chains separated by semicolons
        string[] chains = result.Split(';');
        chains.Should().HaveCount(5);
    }
}
