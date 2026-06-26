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

using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class GpuAccelResolverTests
{
    private static bool HasAll(string _) => true;

    private static bool HasNone(string _) => false;

    [Fact]
    public void Resolve_Nvidia_WithScaleCuda_ReturnsCudaPlan()
    {
        GpuAccelPlan? plan = GpuAccelResolver.Resolve(GpuVendor.Nvidia, HasAll);

        plan.Should().NotBeNull();
        plan!.HwAccelDevice.Should().Be("cuda");
        plan.HwAccelOutputFormat.Should().Be("cuda");
        plan.ScaleFilter.Should().Be("scale_cuda");
    }

    [Fact]
    public void Resolve_Nvidia_WithoutScaleCuda_FallsBackToCpu()
    {
        GpuAccelPlan? plan = GpuAccelResolver.Resolve(GpuVendor.Nvidia, HasNone);

        plan.Should().BeNull("no scale_cuda filter → CPU path");
    }

    [Fact]
    public void Resolve_Intel_WithScaleQsv_ReturnsQsvPlan()
    {
        GpuAccelPlan? plan = GpuAccelResolver.Resolve(GpuVendor.Intel, HasAll);

        plan.Should().NotBeNull();
        plan!.HwAccelDevice.Should().Be("qsv");
        plan.HwAccelOutputFormat.Should().Be("qsv");
        plan.ScaleFilter.Should().Be("scale_qsv");
    }

    [Fact]
    public void Resolve_Amd_FallsBackToCpu()
    {
        // This ffmpeg build has no scale_amf — AMD decodes can offload but there
        // is no GPU scaler, so the fused GPU-resident path is not chosen.
        GpuAccelResolver.Resolve(GpuVendor.Amd, HasAll).Should().BeNull();
    }

    [Fact]
    public void Resolve_Apple_FallsBackToCpu()
    {
        // videotoolbox/scale_vt is a cross-platform future path, absent here.
        GpuAccelResolver.Resolve(GpuVendor.Apple, HasAll).Should().BeNull();
    }

    [Fact]
    public void Resolve_NoGpu_FallsBackToCpu()
    {
        GpuAccelResolver.Resolve(null, HasAll).Should().BeNull();
    }
}
