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

using NoMercy.Encoder.Commands;

namespace NoMercy.Tests.Encoder.Commands;

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

    // ── GPU-accelerated scaling ─────────────────────────────────────────────

    [Fact]
    public void GpuScale_CudaScale_ProducesCorrectFilter()
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale("0:v", "scale_cuda", 1920, 1080, "v0")
            .Build();

        result.Should().Be("[0:v]scale_cuda=1920:1080[v0]");
    }

    [Fact]
    public void GpuScale_QsvScale_ProducesCorrectFilter()
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale("0:v", "scale_qsv", 3840, 2160, "v0")
            .Build();

        result.Should().Be("[0:v]scale_qsv=3840:2160[v0]");
    }

    [Theory]
    [InlineData("scale_cuda")]
    [InlineData("scale_qsv")]
    [InlineData("scale_npp")]
    public void GpuScale_DifferentScalers_AllFormatted(string scaler)
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale("0:v", scaler, 1280, 720, "out")
            .Build();

        result.Should().Contain(scaler);
        result.Should().Contain("1280:720");
    }

    [Fact]
    public void GpuScale_WithExplicitWidth_UsesProvidedDimensions()
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale("input", "scale_cuda", 4096, 2160, "hq")
            .Build();

        result.Should().Contain("4096:2160");
    }

    // ── Tonemap algorithm variations ────────────────────────────────────────

    [Theory]
    [InlineData("hable")]
    [InlineData("mobius")]
    [InlineData("reinhard")]
    [InlineData("bt2390")]
    public void Tonemap_VaryingAlgorithm_IncludesAlgo(string algorithm)
    {
        string result = new FilterGraphBuilder().AddTonemap("0:v", algorithm, "sdr").Build();

        result.Should().Contain($"tonemap={algorithm}");
    }

    [Fact]
    public void Tonemap_Hable_ProducesFullChain()
    {
        string result = new FilterGraphBuilder().AddTonemap("0:v", "hable", "sdr").Build();

        result.Should().Contain("zscale=t=linear");
        result.Should().Contain("tonemap=tonemap=hable");
        result.Should().Contain("zscale=t=bt709:m=bt709:r=tv");
        result.Should().Contain("format=yuv420p");
    }

    [Fact]
    public void LibplaceboTonemap_IncludesAllColorParameters()
    {
        string result = new FilterGraphBuilder()
            .AddLibplaceboTonemap("0:v", "hable", "sdr")
            .Build();

        result.Should().Contain("libplacebo=tonemapping=hable");
        result.Should().Contain("color_primaries=bt709");
        result.Should().Contain("color_trc=bt709");
        result.Should().Contain("colorspace=bt709");
        result.Should().Contain("format=yuv420p");
    }

    [Theory]
    [InlineData("hable")]
    [InlineData("lut3d")]
    [InlineData("mobius")]
    public void LibplaceboTonemap_VaryingAlgorithm_IncludesAlgo(string algorithm)
    {
        string result = new FilterGraphBuilder()
            .AddLibplaceboTonemap("0:v", algorithm, "sdr")
            .Build();

        result.Should().Contain($"libplacebo=tonemapping={algorithm}");
    }

    // ── Deinterlace methods ─────────────────────────────────────────────────

    [Theory]
    [InlineData("yadif")]
    [InlineData("w3fdif")]
    [InlineData("bwdif")]
    public void Deinterlace_DifferentMethods_AllIncluded(string method)
    {
        string result = new FilterGraphBuilder().AddDeinterlace("0:v", "deint", method).Build();

        result.Should().Be($"[0:v]{method}[deint]");
    }

    [Fact]
    public void Deinterlace_DefaultMethod_IsYadif()
    {
        string result = new FilterGraphBuilder().AddDeinterlace("0:v", "deint").Build();

        result.Should().Contain("yadif");
    }

    // ── Crop filter ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1920, 800, 0, 140)]
    [InlineData(1280, 720, 0, 0)]
    [InlineData(1080, 1080, 420, 0)]
    public void Crop_VaryingDimensions_AllFormatted(int w, int h, int x, int y)
    {
        string result = new FilterGraphBuilder().AddCrop("0:v", w, h, x, y, "cropped").Build();

        result.Should().Be($"[0:v]crop={w}:{h}:{x}:{y}[cropped]");
    }

    // ── Split filter with many outputs ──────────────────────────────────────

    [Fact]
    public void Split_ManyOutputs_AllIncluded()
    {
        string[] outputs = ["a", "b", "c", "d", "e"];
        string result = new FilterGraphBuilder().AddSplit("0:v", outputs).Build();

        result.Should().Contain("split=5");
        foreach (string label in outputs)
        {
            result.Should().Contain($"[{label}]");
        }
    }

    // ── ScaleWidth edge case ────────────────────────────────────────────────

    [Theory]
    [InlineData(320)]
    [InlineData(640)]
    [InlineData(1280)]
    [InlineData(1920)]
    public void ScaleWidth_VaryingWidths_MaintainsAspectRatio(int width)
    {
        string result = new FilterGraphBuilder().AddScaleWidth("0:v", width, "scaled").Build();

        result.Should().Contain($"scale={width}:-2");
    }

    // ── Filter chaining: multiple operations ────────────────────────────────

    [Fact]
    public void MultipleOperations_ChainedCorrectly()
    {
        string result = new FilterGraphBuilder()
            .AddDeinterlace("0:v", "deint")
            .AddScale("deint", 1920, 1080, "scaled")
            .AddCrop("scaled", 1920, 1000, 0, 40, "cropped")
            .Build();

        string[] chains = result.Split(';');
        chains.Should().HaveCount(3);
        chains[0].Should().Contain("yadif");
        chains[1].Should().Contain("scale=1920:1080");
        chains[2].Should().Contain("crop=1920:1000:0:40");
    }

    [Fact]
    public void ComplexPipeline_AllOperations()
    {
        string result = new FilterGraphBuilder()
            .AddTonemap("0:v", "hable", "sdr")
            .AddSplit("sdr", ["a", "b"])
            .AddScale("a", 1920, 1080, "v0")
            .AddGpuScale("b", "scale_cuda", 1280, 720, "v1")
            .Build();

        string[] chains = result.Split(';');
        chains.Should().HaveCount(4);
    }

    // ── Empty and single-filter edge cases ──────────────────────────────────

    [Fact]
    public void MultipleFilter_BuildsWithSemicolonSeparator()
    {
        string result = new FilterGraphBuilder()
            .AddFilter("0:v", "eq=brightness=0.1", "brightened")
            .AddFilter("brightened", "hflip", "flipped")
            .Build();

        result.Should().Contain(";");
        string[] chains = result.Split(';');
        chains.Should().HaveCount(2);
    }

    [Fact]
    public void ComplexLabelNames_Handled()
    {
        string result = new FilterGraphBuilder()
            .AddFilter("input_video", "some_filter=param", "output_video_1")
            .Build();

        result.Should().Contain("[input_video]");
        result.Should().Contain("[output_video_1]");
    }
}
