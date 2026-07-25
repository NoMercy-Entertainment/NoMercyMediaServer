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

namespace NoMercy.Tests.Encoder.Hdr;

/// <summary>
/// The tonemap selector is the switchboard between HDR source and SDR
/// output. Wrong choice means either:
///
///   - Picking a filter ffmpeg doesn't have → encode fails.
///   - Falling back to CPU zscale when GPU path exists → 5-10x slower.
///
/// Priority order: libplacebo (Vulkan) → tonemap_opencl (OpenCL) →
/// zscale+tonemap (CPU). Tests pin each branch.
/// </summary>
public class TonemapSelectorTests
{
    private readonly TonemapSelector _selector = new();

    [Fact]
    public void SelectBest_LibplacebolAvailable_PicksLibplacebo()
    {
        // libplacebo is the top of the priority stack — always win when
        // present, even if tonemap_opencl is also available.
        Mock<IFfmpegCapabilities> ffmpeg = new();
        ffmpeg.Setup(f => f.HasFilter("libplacebo")).Returns(true);
        ffmpeg.Setup(f => f.HasFilter("tonemap_opencl")).Returns(true);

        TonemapStrategy result = _selector.SelectBest(NullHardware(), ffmpeg.Object);

        result.Method.Should().Be(TonemapMethod.Libplacebo);
        result.IsGpuAccelerated.Should().BeTrue();
        result.FfmpegFilterChain.Should().Contain("libplacebo=tonemapping=hable");
    }

    [Fact]
    public void SelectBest_NoLibplaceboButOpencl_PicksOpencl()
    {
        Mock<IFfmpegCapabilities> ffmpeg = new();
        ffmpeg.Setup(f => f.HasFilter("libplacebo")).Returns(false);
        ffmpeg.Setup(f => f.HasFilter("tonemap_opencl")).Returns(true);

        TonemapStrategy result = _selector.SelectBest(NullHardware(), ffmpeg.Object);

        result.Method.Should().Be(TonemapMethod.TonemapOpencl);
        result.IsGpuAccelerated.Should().BeTrue();
        result.FfmpegFilterChain.Should().Contain("tonemap_opencl=tonemap=hable");
    }

    [Fact]
    public void SelectBest_NoGpuFilters_FallsBackToZscaleTonemap()
    {
        // CPU fallback — only runs if no GPU path is available. The filter
        // chain is longer (linearize → gbrpf32le → tonemap → back to yuv420p)
        // because zscale has to do color-space conversion explicitly.
        Mock<IFfmpegCapabilities> ffmpeg = new();
        ffmpeg.Setup(f => f.HasFilter(It.IsAny<string>())).Returns(false);

        TonemapStrategy result = _selector.SelectBest(NullHardware(), ffmpeg.Object);

        result.Method.Should().Be(TonemapMethod.ZscaleTonemap);
        result.IsGpuAccelerated.Should().BeFalse();
        result.FfmpegFilterChain.Should().Contain("zscale=t=linear");
        result.FfmpegFilterChain.Should().Contain("tonemap=tonemap=hable");
    }

    [Fact]
    public void SelectBest_NullFfmpegCapabilities_FallsBackToZscale()
    {
        // FfmpegCapabilities hasn't been probed yet — can't know what's
        // available. Safe default is CPU zscale which always works.
        TonemapStrategy result = _selector.SelectBest(NullHardware(), null);

        result.Method.Should().Be(TonemapMethod.ZscaleTonemap);
        result.IsGpuAccelerated.Should().BeFalse();
    }

    [Fact]
    public void SelectBest_FilterChains_AllEndInYuv420p()
    {
        // Every output chain terminates in yuv420p so the result is
        // compatible with the widest range of encoders. Missing this
        // step means the encoder gets a HDR-tagged or 10-bit buffer and
        // either rejects it or produces wrong colors.
        Mock<IFfmpegCapabilities> libplacebo = new();
        libplacebo.Setup(f => f.HasFilter("libplacebo")).Returns(true);

        Mock<IFfmpegCapabilities> opencl = new();
        opencl.Setup(f => f.HasFilter("libplacebo")).Returns(false);
        opencl.Setup(f => f.HasFilter("tonemap_opencl")).Returns(true);

        TonemapStrategy placeboResult = _selector.SelectBest(NullHardware(), libplacebo.Object);
        TonemapStrategy openclResult = _selector.SelectBest(NullHardware(), opencl.Object);
        TonemapStrategy zscaleResult = _selector.SelectBest(NullHardware(), null);

        placeboResult.FfmpegFilterChain.Should().Contain("format=yuv420p");
        // OpenCL uses nv12 (GPU-native) not yuv420p — check it ends in nv12.
        openclResult.FfmpegFilterChain.Should().Contain("format=nv12");
        zscaleResult.FfmpegFilterChain.Should().EndWith("format=yuv420p");
    }

    private static IHardwareCapabilities NullHardware()
    {
        Mock<IHardwareCapabilities> hw = new();
        hw.Setup(h => h.Gpus).Returns([]);
        return hw.Object;
    }
}
