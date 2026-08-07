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
    private static readonly GpuAccelPlan GpuResident = new("cuda", "cuda", "scale_cuda");

    private static VideoOutputPlan VideoWith(
        string encoderName,
        bool convertHdrToSdr = false,
        string? tonemapFilterChain = null
    ) =>
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
            ExtraFlags: [],
            ConvertHdrToSdr: convertHdrToSdr,
            TonemapFilterChain: tonemapFilterChain
        );

    // ── GPU encoder detection ──────────────────────────────────────────────

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    [InlineData("vp9_qsv")]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    [InlineData("av1_vaapi")]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    [InlineData("h264_cuvid")] // decoder, but treated as GPU too
    public void ForVideoOutput_GpuEncoder_ReservesGpuSlot(string encoderName)
    {
        VideoOutputPlan plan = VideoWith(encoderName);
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, GpuResident);

        req.GpuSlots.Should().Be(1);
        req.GpuDeviceKey.Should().Be(encoderName);
        req.CpuThreads.Should().Be(2); // decode + scale live on the GPU; only helper threads remain
    }

    // ── CPU cost of a GPU encode ────────────────────────────────────────────
    //
    // A GPU encoder does not mean a free CPU. Unless decode AND scaling are
    // GPU-resident, ffmpeg still decodes and filters every frame on the CPU —
    // measurably ~20% of a desktop box for one 1080p nvenc encode. Reserving a
    // flat 2 threads let the scheduler start a full software encode alongside
    // it and peg the machine.

    [Fact]
    public void ForVideoOutput_GpuEncoder_WithCpuFilterGraph_ReservesQuarterOfTheCores()
    {
        VideoOutputPlan plan = VideoWith("h264_nvenc");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        req.GpuSlots.Should().Be(1);
        req.CpuThreads.Should().Be(Math.Max(3, Environment.ProcessorCount / 4));
    }

    [Fact]
    public void ForVideoOutput_GpuEncoder_WithCpuFilterGraph_CostsMoreThanGpuResident()
    {
        VideoOutputPlan plan = VideoWith("h264_nvenc");

        int cpuGraph = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null).CpuThreads;
        int gpuGraph = TaskResourceHelper.ForVideoOutput(plan, GpuResident).CpuThreads;

        cpuGraph.Should().BeGreaterThan(gpuGraph);
    }

    [Fact]
    public void ForVideoOutput_GpuEncoder_WithCpuTonemap_CostsAsMuchAsSoftware()
    {
        // zscale/tonemap on the CPU is the dominant cost of an HDR→SDR pass —
        // the encoder being nvenc does not make it cheaper.
        VideoOutputPlan plan = VideoWith(
            "h264_nvenc",
            convertHdrToSdr: true,
            tonemapFilterChain: "zscale=t=linear,tonemap=hable,zscale=t=bt709"
        );
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        req.GpuSlots.Should().Be(1);
        req.CpuThreads.Should().Be(Math.Max(2, Environment.ProcessorCount / 2));
    }

    [Fact]
    public void ForVideoOutput_GpuEncoder_WithGpuResidentTonemap_StaysCheap()
    {
        VideoOutputPlan plan = VideoWith(
            "h264_nvenc",
            convertHdrToSdr: true,
            tonemapFilterChain: "tonemap_cuda=tonemap=hable"
        );
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, GpuResident);

        req.CpuThreads.Should().Be(2);
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    [InlineData("libaom-av1")]
    [InlineData("libvpx-vp9")]
    public void ForVideoOutput_SoftwareEncoder_ReservesCpuOnly(string encoderName)
    {
        VideoOutputPlan plan = VideoWith(encoderName);
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        req.GpuSlots.Should().Be(0);
        req.GpuDeviceKey.Should().BeNull();
        req.CpuThreads.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ForVideoOutput_SoftwareEncoder_UsesHalfTheCpuCores()
    {
        VideoOutputPlan plan = VideoWith("libx264");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        int expected = Math.Max(1, Environment.ProcessorCount / 2);
        req.CpuThreads.Should().Be(expected);
    }

    [Fact]
    public void ForVideoOutput_EmptyEncoder_TreatedAsSoftware()
    {
        VideoOutputPlan plan = VideoWith("");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        req.GpuSlots.Should().Be(0);
        req.GpuDeviceKey.Should().BeNull();
    }

    [Fact]
    public void ForVideoOutput_UnknownEncoder_TreatedAsSoftware()
    {
        VideoOutputPlan plan = VideoWith("totally_made_up_encoder");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        req.GpuSlots.Should().Be(0);
    }

    [Fact]
    public void ForVideoOutput_CaseInsensitive_StillDetectsGpu()
    {
        // Encoder names from upstream sources may differ in case.
        VideoOutputPlan plan = VideoWith("H264_NVENC");
        ResourceRequirement req = TaskResourceHelper.ForVideoOutput(plan, gpuAccel: null);

        req.GpuSlots.Should().Be(1);
    }

    // ── CpuOnly helper ──────────────────────────────────────────────────────

    [Fact]
    public void CpuOnly_DefaultsToOneThread()
    {
        ResourceRequirement req = TaskResourceHelper.CpuOnly();

        req.GpuSlots.Should().Be(0);
        req.GpuDeviceKey.Should().BeNull();
        req.CpuThreads.Should().Be(1);
    }

    [Fact]
    public void CpuOnly_CustomThreadCount_FlowsThrough()
    {
        ResourceRequirement req = TaskResourceHelper.CpuOnly(cpuThreads: 8);

        req.CpuThreads.Should().Be(8);
    }
}
