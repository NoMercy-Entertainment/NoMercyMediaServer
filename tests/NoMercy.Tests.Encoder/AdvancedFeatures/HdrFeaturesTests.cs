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

using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;

namespace NoMercy.Tests.Encoder.AdvancedFeatures;

public class HdrFeaturesTests
{
    private readonly Mock<IHardwareCapabilities> _hardwareMock = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegMock = new();

    [Fact]
    public void TonemapSelector_SelectsLibplacebo_WhenFilterAvailable()
    {
        _ffmpegMock.Setup(expression: f => f.HasFilter("libplacebo")).Returns(value: true);
        _ffmpegMock.Setup(expression: f => f.HasFilter("tonemap_opencl")).Returns(value: false);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(hardware: _hardwareMock.Object, ffmpeg: _ffmpegMock.Object);

        strategy.Method.Should().Be(expected: TonemapMethod.Libplacebo);
        strategy.IsGpuAccelerated.Should().BeTrue();
        strategy.FfmpegFilterChain.Should().Contain(expected: "libplacebo");
    }

    [Fact]
    public void TonemapSelector_SelectsTonemapOpencl_WhenLibplaceboNotAvailable()
    {
        _ffmpegMock.Setup(expression: f => f.HasFilter("libplacebo")).Returns(value: false);
        _ffmpegMock.Setup(expression: f => f.HasFilter("tonemap_opencl")).Returns(value: true);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(hardware: _hardwareMock.Object, ffmpeg: _ffmpegMock.Object);

        strategy.Method.Should().Be(expected: TonemapMethod.TonemapOpencl);
        strategy.IsGpuAccelerated.Should().BeTrue();
        strategy.FfmpegFilterChain.Should().Contain(expected: "tonemap_opencl");
    }

    [Fact]
    public void TonemapSelector_FallsBackToZscale_WhenNoGpuFiltersAvailable()
    {
        _ffmpegMock.Setup(expression: f => f.HasFilter("libplacebo")).Returns(value: false);
        _ffmpegMock.Setup(expression: f => f.HasFilter("tonemap_opencl")).Returns(value: false);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(hardware: _hardwareMock.Object, ffmpeg: _ffmpegMock.Object);

        strategy.Method.Should().Be(expected: TonemapMethod.ZscaleTonemap);
        strategy.IsGpuAccelerated.Should().BeFalse();
        strategy.FfmpegFilterChain.Should().Contain(expected: "zscale");
    }

    [Fact]
    public void TonemapSelector_LibplaceboTakesPriorityOverOpencl()
    {
        _ffmpegMock.Setup(expression: f => f.HasFilter("libplacebo")).Returns(value: true);
        _ffmpegMock.Setup(expression: f => f.HasFilter("tonemap_opencl")).Returns(value: true);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(hardware: _hardwareMock.Object, ffmpeg: _ffmpegMock.Object);

        strategy.Method.Should().Be(expected: TonemapMethod.Libplacebo);
    }

    [Fact]
    public void TonemapStrategy_ConstructsCorrectly()
    {
        TonemapStrategy strategy = new(
            Method: TonemapMethod.CustomLut,
            FfmpegFilterChain: "lut3d=file=custom.cube",
            IsGpuAccelerated: false
        );

        strategy.Method.Should().Be(expected: TonemapMethod.CustomLut);
        strategy.FfmpegFilterChain.Should().Be(expected: "lut3d=file=custom.cube");
        strategy.IsGpuAccelerated.Should().BeFalse();
    }

    [Fact]
    public void TonemapMethod_HasExpectedValues()
    {
        TonemapMethod[] values = Enum.GetValues<TonemapMethod>();

        values.Should().Contain(expected: TonemapMethod.Libplacebo);
        values.Should().Contain(expected: TonemapMethod.TonemapOpencl);
        values.Should().Contain(expected: TonemapMethod.ZscaleTonemap);
        values.Should().Contain(expected: TonemapMethod.CustomLut);
        values.Should().HaveCount(expected: 4);
    }

    [Fact]
    public void HdrOptions_DefaultValues_AreCorrect()
    {
        HdrOptions options = new();

        options.Algorithm.Should().Be(expected: TonemapAlgorithm.Hable);
        options.CustomLutPath.Should().BeNull();
        options.LutApply.Should().Be(expected: LutApplication.AfterTonemap);
        options.Desat.Should().Be(expected: 0.0);
        options.Peak.Should().Be(expected: 0.0);
        options.PreserveMetadata.Should().BeFalse();
    }

    [Fact]
    public void HdrOptions_ConstructsWithCustomValues()
    {
        HdrOptions options = new(
            Algorithm: TonemapAlgorithm.Reinhard,
            CustomLutPath: "/luts/custom.cube",
            LutApply: LutApplication.BeforeTonemap,
            Desat: 0.5,
            Peak: 1000.0,
            PreserveMetadata: true
        );

        options.Algorithm.Should().Be(expected: TonemapAlgorithm.Reinhard);
        options.CustomLutPath.Should().Be(expected: "/luts/custom.cube");
        options.LutApply.Should().Be(expected: LutApplication.BeforeTonemap);
        options.Desat.Should().BeApproximately(expectedValue: 0.5, precision: 0.001);
        options.Peak.Should().BeApproximately(expectedValue: 1000.0, precision: 0.001);
        options.PreserveMetadata.Should().BeTrue();
    }

    [Fact]
    public void TonemapAlgorithm_HasAllExpectedValues()
    {
        TonemapAlgorithm[] values = Enum.GetValues<TonemapAlgorithm>();

        values.Should().Contain(expected: TonemapAlgorithm.Hable);
        values.Should().Contain(expected: TonemapAlgorithm.Reinhard);
        values.Should().Contain(expected: TonemapAlgorithm.Mobius);
        values.Should().Contain(expected: TonemapAlgorithm.Bt2390);
        values.Should().HaveCount(expected: 4);
    }

    [Fact]
    public void LutApplication_HasAllExpectedValues()
    {
        LutApplication[] values = Enum.GetValues<LutApplication>();

        values.Should().Contain(expected: LutApplication.BeforeTonemap);
        values.Should().Contain(expected: LutApplication.AfterTonemap);
        values.Should().Contain(expected: LutApplication.InsteadOfTonemap);
        values.Should().HaveCount(expected: 3);
    }

    [Fact]
    public void ZscaleFilterChain_ContainsTonemap()
    {
        _ffmpegMock.Setup(expression: f => f.HasFilter("libplacebo")).Returns(value: false);
        _ffmpegMock.Setup(expression: f => f.HasFilter("tonemap_opencl")).Returns(value: false);

        TonemapSelector selector = new();
        TonemapStrategy strategy = selector.SelectBest(hardware: _hardwareMock.Object, ffmpeg: _ffmpegMock.Object);

        strategy.FfmpegFilterChain.Should().Contain(expected: "tonemap=tonemap=hable");
        strategy.FfmpegFilterChain.Should().Contain(expected: "yuv420p");
    }
}
