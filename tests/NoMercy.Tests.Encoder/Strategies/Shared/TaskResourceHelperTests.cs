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

using NoMercy.Encoder.Output;
using NoMercy.Encoder.Strategies.Shared;

namespace NoMercy.Tests.Encoder.Strategies.Shared;

/// <summary>
/// TaskResourceHelper decides whether a decomposed task needs a GPU slot or
/// just CPU threads. Wrong classification means a CPU-only encode reserves
/// GPU slots it never uses (stall), or a GPU encode runs without a slot
/// reservation (oversubscribes the engine).
/// </summary>
public class TaskResourceHelperTests
{
    private static VideoOutputPlan VideoWith(string encoderName) =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: encoderName,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "main",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v]",
            ExtraFlags: []
        );

    // ── GPU encoder detection ──────────────────────────────────────────────

    [Theory]
    [InlineData(data: "h264_nvenc")]
    [InlineData(data: "hevc_nvenc")]
    [InlineData(data: "av1_nvenc")]
    [InlineData(data: "h264_amf")]
    [InlineData(data: "hevc_amf")]
    [InlineData(data: "h264_qsv")]
    [InlineData(data: "hevc_qsv")]
    [InlineData(data: "av1_qsv")]
    [InlineData(data: "vp9_qsv")]
    [InlineData(data: "h264_vaapi")]
    [InlineData(data: "hevc_vaapi")]
    [InlineData(data: "av1_vaapi")]
    [InlineData(data: "h264_videotoolbox")]
    [InlineData(data: "hevc_videotoolbox")]
    [InlineData(data: "h264_cuvid")] // decoder, but treated as GPU too
    public void ForVideoOutput_GpuEncoder_ReservesGpuSlot(string encoderName)
    {
        VideoOutputPlan plan = VideoWith(encoderName: encoderName);
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(video: plan);

        req.GpuSlots.Should().Be(expected: 1);
        req.GpuDeviceKey.Should().Be(expected: encoderName);
        req.CpuThreads.Should().Be(expected: 2); // GPU encodes still spawn 2 CPU helper threads
    }

    [Theory]
    [InlineData(data: "libx264")]
    [InlineData(data: "libx265")]
    [InlineData(data: "libsvtav1")]
    [InlineData(data: "libaom-av1")]
    [InlineData(data: "libvpx-vp9")]
    public void ForVideoOutput_SoftwareEncoder_ReservesCpuOnly(string encoderName)
    {
        VideoOutputPlan plan = VideoWith(encoderName: encoderName);
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(video: plan);

        req.GpuSlots.Should().Be(expected: 0);
        req.GpuDeviceKey.Should().BeNull();
        req.CpuThreads.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public void ForVideoOutput_SoftwareEncoder_UsesHalfTheCpuCores()
    {
        VideoOutputPlan plan = VideoWith(encoderName: "libx264");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(video: plan);

        int expected = Math.Max(val1: 1, val2: Environment.ProcessorCount / 2);
        req.CpuThreads.Should().Be(expected: expected);
    }

    [Fact]
    public void ForVideoOutput_EmptyEncoder_TreatedAsSoftware()
    {
        VideoOutputPlan plan = VideoWith(encoderName: "");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(video: plan);

        req.GpuSlots.Should().Be(expected: 0);
        req.GpuDeviceKey.Should().BeNull();
    }

    [Fact]
    public void ForVideoOutput_UnknownEncoder_TreatedAsSoftware()
    {
        VideoOutputPlan plan = VideoWith(encoderName: "totally_made_up_encoder");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(video: plan);

        req.GpuSlots.Should().Be(expected: 0);
    }

    [Fact]
    public void ForVideoOutput_CaseInsensitive_StillDetectsGpu()
    {
        // Encoder names from upstream sources may differ in case.
        VideoOutputPlan plan = VideoWith(encoderName: "H264_NVENC");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(video: plan);

        req.GpuSlots.Should().Be(expected: 1);
    }

    // ── CpuOnly helper ──────────────────────────────────────────────────────

    [Fact]
    public void CpuOnly_DefaultsToOneThread()
    {
        ResourceRequirement req = TaskResourceHelper.CpuOnly();

        req.GpuSlots.Should().Be(expected: 0);
        req.GpuDeviceKey.Should().BeNull();
        req.CpuThreads.Should().Be(expected: 1);
    }

    [Fact]
    public void CpuOnly_CustomThreadCount_FlowsThrough()
    {
        ResourceRequirement req = TaskResourceHelper.CpuOnly(cpuThreads: 8);

        req.CpuThreads.Should().Be(expected: 8);
    }
}
