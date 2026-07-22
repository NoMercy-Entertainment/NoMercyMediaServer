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
        string result = new FilterGraphBuilder().AddScale(inputLabel: "0:v", width: 1920, height: 1080, outputLabel: "v0").Build();

        result.Should().Be(expected: "[0:v]scale=1920:1080[v0]");
    }

    [Fact]
    public void SplitAndMultiScale_ProducesCorrectOutput()
    {
        string result = new FilterGraphBuilder()
            .AddSplit(inputLabel: "0:v", outputLabels: ["a", "b", "c"])
            .AddScale(inputLabel: "a", width: 3840, height: 2160, outputLabel: "v0")
            .AddScale(inputLabel: "b", width: 1920, height: 1080, outputLabel: "v1")
            .AddScale(inputLabel: "c", width: 1280, height: 720, outputLabel: "v2")
            .Build();

        result
            .Should()
            .Be(
                expected: "[0:v]split=3[a][b][c];[a]scale=3840:2160[v0];[b]scale=1920:1080[v1];[c]scale=1280:720[v2]"
            );
    }

    [Fact]
    public void TonemapThenSplitThenScale_ProducesChain()
    {
        string result = new FilterGraphBuilder()
            .AddTonemap(inputLabel: "0:v", algorithm: "hable", outputLabel: "sdr")
            .AddSplit(inputLabel: "sdr", outputLabels: ["a", "b"])
            .AddScale(inputLabel: "a", width: 1920, height: 1080, outputLabel: "v0")
            .AddScale(inputLabel: "b", width: 1280, height: 720, outputLabel: "v1")
            .Build();

        result.Should().StartWith(expected: "[0:v]zscale=t=linear");
        result.Should().Contain(expected: "[sdr]");
        result.Should().Contain(expected: "[sdr]split=2[a][b]");
        result.Should().Contain(expected: "[a]scale=1920:1080[v0]");
        result.Should().Contain(expected: "[b]scale=1280:720[v1]");
    }

    [Fact]
    public void LibplaceboTonemap_ProducesCorrectFilter()
    {
        string result = new FilterGraphBuilder()
            .AddLibplaceboTonemap(inputLabel: "0:v", algorithm: "hable", outputLabel: "sdr")
            .Build();

        result.Should().Contain(expected: "libplacebo=tonemapping=hable");
        result.Should().Contain(expected: "color_primaries=bt709");
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
        builder.AddScale(inputLabel: "0:v", width: 1920, height: 1080, outputLabel: "v0");
        builder.HasFilters.Should().BeTrue();
    }

    [Fact]
    public void Deinterlace_ProducesYadif()
    {
        string result = new FilterGraphBuilder().AddDeinterlace(inputLabel: "0:v", outputLabel: "deint").Build();

        result.Should().Be(expected: "[0:v]yadif[deint]");
    }

    [Fact]
    public void Crop_ProducesCorrectParams()
    {
        string result = new FilterGraphBuilder()
            .AddCrop(inputLabel: "0:v", width: 1920, height: 800, x: 0, y: 140, outputLabel: "cropped")
            .Build();

        result.Should().Be(expected: "[0:v]crop=1920:800:0:140[cropped]");
    }

    [Fact]
    public void ScaleWidth_UsesMinusTwoForHeight()
    {
        string result = new FilterGraphBuilder().AddScaleWidth(inputLabel: "0:v", width: 1280, outputLabel: "v0").Build();

        result.Should().Be(expected: "[0:v]scale=1280:-2[v0]");
    }

    [Fact]
    public void ComplexChain_4kHdrToMultiSdr()
    {
        // Simulate: 4K HDR → tonemap → split 3 → scale to 4K/1080p/720p
        string result = new FilterGraphBuilder()
            .AddTonemap(inputLabel: "0:v", algorithm: "hable", outputLabel: "sdr")
            .AddSplit(inputLabel: "sdr", outputLabels: ["a", "b", "c"])
            .AddScale(inputLabel: "a", width: 3840, height: 2160, outputLabel: "v0")
            .AddScale(inputLabel: "b", width: 1920, height: 1080, outputLabel: "v1")
            .AddScale(inputLabel: "c", width: 1280, height: 720, outputLabel: "v2")
            .Build();

        // Should have 5 chains separated by semicolons
        string[] chains = result.Split(separator: ';');
        chains.Should().HaveCount(expected: 5);
    }

    // ── GPU-accelerated scaling ─────────────────────────────────────────────

    [Fact]
    public void GpuScale_CudaScale_ProducesCorrectFilter()
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale(inputLabel: "0:v", scaleFilter: "scale_cuda", width: 1920, height: 1080, outputLabel: "v0")
            .Build();

        result.Should().Be(expected: "[0:v]scale_cuda=1920:1080[v0]");
    }

    [Fact]
    public void GpuScale_QsvScale_ProducesCorrectFilter()
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale(inputLabel: "0:v", scaleFilter: "scale_qsv", width: 3840, height: 2160, outputLabel: "v0")
            .Build();

        result.Should().Be(expected: "[0:v]scale_qsv=3840:2160[v0]");
    }

    [Theory]
    [InlineData(data: "scale_cuda")]
    [InlineData(data: "scale_qsv")]
    [InlineData(data: "scale_npp")]
    public void GpuScale_DifferentScalers_AllFormatted(string scaler)
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale(inputLabel: "0:v", scaleFilter: scaler, width: 1280, height: 720, outputLabel: "out")
            .Build();

        result.Should().Contain(expected: scaler);
        result.Should().Contain(expected: "1280:720");
    }

    [Fact]
    public void GpuScale_WithExplicitWidth_UsesProvidedDimensions()
    {
        string result = new FilterGraphBuilder()
            .AddGpuScale(inputLabel: "input", scaleFilter: "scale_cuda", width: 4096, height: 2160, outputLabel: "hq")
            .Build();

        result.Should().Contain(expected: "4096:2160");
    }

    // ── Tonemap algorithm variations ────────────────────────────────────────

    [Theory]
    [InlineData(data: "hable")]
    [InlineData(data: "mobius")]
    [InlineData(data: "reinhard")]
    [InlineData(data: "bt2390")]
    public void Tonemap_VaryingAlgorithm_IncludesAlgo(string algorithm)
    {
        string result = new FilterGraphBuilder().AddTonemap(inputLabel: "0:v", algorithm: algorithm, outputLabel: "sdr").Build();

        result.Should().Contain(expected: $"tonemap={algorithm}");
    }

    [Fact]
    public void Tonemap_Hable_ProducesFullChain()
    {
        string result = new FilterGraphBuilder().AddTonemap(inputLabel: "0:v", algorithm: "hable", outputLabel: "sdr").Build();

        result.Should().Contain(expected: "zscale=t=linear");
        result.Should().Contain(expected: "tonemap=tonemap=hable");
        result.Should().Contain(expected: "zscale=t=bt709:m=bt709:r=tv");
        result.Should().Contain(expected: "format=yuv420p");
    }

    [Fact]
    public void LibplaceboTonemap_IncludesAllColorParameters()
    {
        string result = new FilterGraphBuilder()
            .AddLibplaceboTonemap(inputLabel: "0:v", algorithm: "hable", outputLabel: "sdr")
            .Build();

        result.Should().Contain(expected: "libplacebo=tonemapping=hable");
        result.Should().Contain(expected: "color_primaries=bt709");
        result.Should().Contain(expected: "color_trc=bt709");
        result.Should().Contain(expected: "colorspace=bt709");
        result.Should().Contain(expected: "format=yuv420p");
    }

    [Theory]
    [InlineData(data: "hable")]
    [InlineData(data: "lut3d")]
    [InlineData(data: "mobius")]
    public void LibplaceboTonemap_VaryingAlgorithm_IncludesAlgo(string algorithm)
    {
        string result = new FilterGraphBuilder()
            .AddLibplaceboTonemap(inputLabel: "0:v", algorithm: algorithm, outputLabel: "sdr")
            .Build();

        result.Should().Contain(expected: $"libplacebo=tonemapping={algorithm}");
    }

    // ── Deinterlace methods ─────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "yadif")]
    [InlineData(data: "w3fdif")]
    [InlineData(data: "bwdif")]
    public void Deinterlace_DifferentMethods_AllIncluded(string method)
    {
        string result = new FilterGraphBuilder().AddDeinterlace(inputLabel: "0:v", outputLabel: "deint", method: method).Build();

        result.Should().Be(expected: $"[0:v]{method}[deint]");
    }

    [Fact]
    public void Deinterlace_DefaultMethod_IsYadif()
    {
        string result = new FilterGraphBuilder().AddDeinterlace(inputLabel: "0:v", outputLabel: "deint").Build();

        result.Should().Contain(expected: "yadif");
    }

    // ── Crop filter ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: [1920, 800, 0, 140])]
    [InlineData(data: [1280, 720, 0, 0])]
    [InlineData(data: [1080, 1080, 420, 0])]
    public void Crop_VaryingDimensions_AllFormatted(int w, int h, int x, int y)
    {
        string result = new FilterGraphBuilder().AddCrop(inputLabel: "0:v", width: w, height: h, x: x, y: y, outputLabel: "cropped").Build();

        result.Should().Be(expected: $"[0:v]crop={w}:{h}:{x}:{y}[cropped]");
    }

    // ── Split filter with many outputs ──────────────────────────────────────

    [Fact]
    public void Split_ManyOutputs_AllIncluded()
    {
        string[] outputs = ["a", "b", "c", "d", "e"];
        string result = new FilterGraphBuilder().AddSplit(inputLabel: "0:v", outputLabels: outputs).Build();

        result.Should().Contain(expected: "split=5");
        foreach (string label in outputs)
        {
            result.Should().Contain(expected: $"[{label}]");
        }
    }

    // ── ScaleWidth edge case ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: 320)]
    [InlineData(data: 640)]
    [InlineData(data: 1280)]
    [InlineData(data: 1920)]
    public void ScaleWidth_VaryingWidths_MaintainsAspectRatio(int width)
    {
        string result = new FilterGraphBuilder().AddScaleWidth(inputLabel: "0:v", width: width, outputLabel: "scaled").Build();

        result.Should().Contain(expected: $"scale={width}:-2");
    }

    // ── Filter chaining: multiple operations ────────────────────────────────

    [Fact]
    public void MultipleOperations_ChainedCorrectly()
    {
        string result = new FilterGraphBuilder()
            .AddDeinterlace(inputLabel: "0:v", outputLabel: "deint")
            .AddScale(inputLabel: "deint", width: 1920, height: 1080, outputLabel: "scaled")
            .AddCrop(inputLabel: "scaled", width: 1920, height: 1000, x: 0, y: 40, outputLabel: "cropped")
            .Build();

        string[] chains = result.Split(separator: ';');
        chains.Should().HaveCount(expected: 3);
        chains[0].Should().Contain(expected: "yadif");
        chains[1].Should().Contain(expected: "scale=1920:1080");
        chains[2].Should().Contain(expected: "crop=1920:1000:0:40");
    }

    [Fact]
    public void ComplexPipeline_AllOperations()
    {
        string result = new FilterGraphBuilder()
            .AddTonemap(inputLabel: "0:v", algorithm: "hable", outputLabel: "sdr")
            .AddSplit(inputLabel: "sdr", outputLabels: ["a", "b"])
            .AddScale(inputLabel: "a", width: 1920, height: 1080, outputLabel: "v0")
            .AddGpuScale(inputLabel: "b", scaleFilter: "scale_cuda", width: 1280, height: 720, outputLabel: "v1")
            .Build();

        string[] chains = result.Split(separator: ';');
        chains.Should().HaveCount(expected: 4);
    }

    // ── Empty and single-filter edge cases ──────────────────────────────────

    [Fact]
    public void MultipleFilter_BuildsWithSemicolonSeparator()
    {
        string result = new FilterGraphBuilder()
            .AddFilter(inputLabel: "0:v", filter: "eq=brightness=0.1", outputLabel: "brightened")
            .AddFilter(inputLabel: "brightened", filter: "hflip", outputLabel: "flipped")
            .Build();

        result.Should().Contain(expected: ";");
        string[] chains = result.Split(separator: ';');
        chains.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void ComplexLabelNames_Handled()
    {
        string result = new FilterGraphBuilder()
            .AddFilter(inputLabel: "input_video", filter: "some_filter=param", outputLabel: "output_video_1")
            .Build();

        result.Should().Contain(expected: "[input_video]");
        result.Should().Contain(expected: "[output_video_1]");
    }
}
