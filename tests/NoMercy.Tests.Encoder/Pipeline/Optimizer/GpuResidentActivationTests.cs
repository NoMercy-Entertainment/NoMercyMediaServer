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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.PostProcess;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class GpuResidentActivationTests
{
    private static bool HasCuda(string filter) => filter == "scale_cuda";

    [Fact]
    public void Resolve_EnabledNvidiaEligible_ReturnsCudaPlan()
    {
        GpuAccelPlan? plan = GpuResidentActivation.Resolve(
            enabled: true,
            hasGpu: true,
            vendor: GpuVendor.Nvidia,
            plan: EligiblePlan(),
            hasFilter: HasCuda
        );

        plan.Should().NotBeNull();
        plan!.ScaleFilter.Should().Be("scale_cuda");
    }

    [Fact]
    public void Resolve_Disabled_ReturnsNull()
    {
        GpuResidentActivation
            .Resolve(false, true, GpuVendor.Nvidia, EligiblePlan(), HasCuda)
            .Should()
            .BeNull("dark by default — opt-in required");
    }

    [Fact]
    public void Resolve_NoGpu_ReturnsNull()
    {
        GpuResidentActivation.Resolve(true, false, null, EligiblePlan(), HasCuda).Should().BeNull();
    }

    [Fact]
    public void Resolve_WithThumbnails_ReturnsNull()
    {
        OutputPlan withThumbs = EligiblePlan() with
        {
            Thumbnails = new(320, 180, 10, SpriteGrid.For(TimeSpan.FromHours(2), 10)),
        };
        GpuResidentActivation
            .Resolve(true, true, GpuVendor.Nvidia, withThumbs, HasCuda)
            .Should()
            .BeNull("sprite generation needs a CPU download");
    }

    [Fact]
    public void Resolve_HdrTonemapInPlan_ReturnsNull()
    {
        OutputPlan hdr = EligiblePlan();
        hdr = hdr with { VideoOutputs = [hdr.VideoOutputs[0] with { ConvertHdrToSdr = true }] };
        GpuResidentActivation.Resolve(true, true, GpuVendor.Nvidia, hdr, HasCuda).Should().BeNull();
    }

    private static OutputPlan EligiblePlan() =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "h264_nvenc",
                    Crf: 23,
                    BitrateKbps: 5000,
                    Preset: "p4",
                    Profile: "high",
                    Level: "4.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
