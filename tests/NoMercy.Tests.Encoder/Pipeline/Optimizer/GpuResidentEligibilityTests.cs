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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class GpuResidentEligibilityTests
{
    [Fact]
    public void Eligible_PureDecodeScaleEncode_True()
    {
        bool eligible = GpuResidentEligibility.IsEligible([Video()], []);
        eligible.Should().BeTrue("decode→scale→encode keeps frames on the GPU");
    }

    [Fact]
    public void Ineligible_HdrTonemap_False()
    {
        VideoOutputPlan hdr = Video() with { ConvertHdrToSdr = true };
        GpuResidentEligibility.IsEligible([hdr], []).Should().BeFalse("CPU tonemap in the graph");
    }

    [Fact]
    public void Ineligible_Crop_False()
    {
        VideoOutputPlan cropped = Video() with { CropFilter = "1920:800:0:140" };
        GpuResidentEligibility.IsEligible([cropped], []).Should().BeFalse();
    }

    [Fact]
    public void Ineligible_SubtitleBurnIn_False()
    {
        SubtitleOutputPlan burnIn = new(
            SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "en",
            SourceIndex: 0,
            MapLabel: null,
            Policy: SubtitlePolicy.BurnIn
        );
        GpuResidentEligibility.IsEligible([Video()], [burnIn]).Should().BeFalse();
    }

    [Fact]
    public void Ineligible_NoVideo_False()
    {
        GpuResidentEligibility.IsEligible([], []).Should().BeFalse();
    }

    private static VideoOutputPlan Video() =>
        new(
            1920,
            1080,
            "h264_nvenc",
            23,
            5000,
            "p4",
            "high",
            "4.1",
            false,
            "yuv420p",
            "[v0]",
            new()
        );
}
