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
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Optimizer;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class EncodeCostModelTests
{
    private static readonly SpeedIndex EmptySpeed = new(Measurements: []);

    [Fact]
    public void RungCost_HigherResolution_CostsMore()
    {
        EncodeCostModel model = new(speedIndex: EmptySpeed);

        double uhd = model.RungCost(width: 3840, height: 2160, codec: VideoCodecType.H265, encoder: "libx265", passes: 1);
        double fhd = model.RungCost(width: 1920, height: 1080, codec: VideoCodecType.H265, encoder: "libx265", passes: 1);

        uhd.Should().BeGreaterThan(expected: fhd, because: "more pixels = more encode work");
    }

    [Fact]
    public void RungCost_TwoPass_DoublesSinglePass()
    {
        EncodeCostModel model = new(speedIndex: EmptySpeed);

        double single = model.RungCost(width: 1920, height: 1080, codec: VideoCodecType.H264, encoder: "libx264", passes: 1);
        double twoPass = model.RungCost(width: 1920, height: 1080, codec: VideoCodecType.H264, encoder: "libx264", passes: 2);

        twoPass.Should().BeApproximately(expectedValue: single * 2, precision: 0.001);
    }

    [Fact]
    public void RungCost_FasterEncoder_CostsLess()
    {
        // A benchmarked encoder with 4x realtime is cheaper than the 1.0 default.
        SpeedIndex fast = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: null)] = new(
                    Fps: 120,
                    SpeedMultiplier: 4.0,
                    MeasuredAt: default
                ),
            }
        );
        EncodeCostModel hw = new(speedIndex: fast);
        EncodeCostModel sw = new(speedIndex: EmptySpeed);

        double hwCost = hw.RungCost(width: 1920, height: 1080, codec: VideoCodecType.H265, encoder: "hevc_nvenc", passes: 1);
        double swCost = sw.RungCost(width: 1920, height: 1080, codec: VideoCodecType.H265, encoder: "libx265", passes: 1);

        hwCost.Should().BeLessThan(expected: swCost, because: "a 4x-realtime encoder costs less wall-time");
    }

    [Fact]
    public void TotalCost_SumsRungsPlusDecodeAndTonemap()
    {
        EncodeCostModel model = new(speedIndex: EmptySpeed);

        double withTonemap = model.TotalCost(
            sourceWidth: 3840,
            sourceHeight: 2160,
            sourceIsHdr: true,
            rungs:
            [
                new(Width: 1920, Height: 1080, Codec: VideoCodecType.H265, Encoder: "libx265", Passes: 1),
                new(Width: 1280, Height: 720, Codec: VideoCodecType.H265, Encoder: "libx265", Passes: 1),
            ]
        );
        double withoutTonemap = model.TotalCost(
            sourceWidth: 3840,
            sourceHeight: 2160,
            sourceIsHdr: false,
            rungs:
            [
                new(Width: 1920, Height: 1080, Codec: VideoCodecType.H265, Encoder: "libx265", Passes: 1),
                new(Width: 1280, Height: 720, Codec: VideoCodecType.H265, Encoder: "libx265", Passes: 1),
            ]
        );

        withTonemap.Should().BeGreaterThan(expected: withoutTonemap, because: "HDR adds a tonemap pass");
    }
}
