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

namespace NoMercy.Encoder.Strategies.Shared;

/// <summary>
/// Derives <see cref="ResourceRequirement"/> for a decomposed task from the
/// output plan. GPU is detected by encoder name suffix — any encoder whose
/// name contains a hardware-acceleration token (nvenc, amf, qsv, vaapi,
/// videotoolbox, cuvid) gets a GPU-slot requirement.
///
/// A GPU encoder does NOT mean a free CPU. The GPU slot only covers the encode
/// itself; unless <see cref="GpuAccelPlan"/> puts decode and scaling on the GPU
/// too, ffmpeg still decodes and filters every frame on the CPU. Reserving a
/// flat 2 threads for those runs let the scheduler start a full software encode
/// alongside a GPU one and peg the host, which is the whole failure this class
/// exists to prevent — so the CPU reservation is derived from where the filter
/// graph actually runs, not from the encoder's name.
/// </summary>
internal static class TaskResourceHelper
{
    private static readonly IReadOnlyList<string> GpuEncoderTokens = Hardware
        .GpuEncoderTokens
        .VendorPrefixes;

    private static int SoftwareEncodeThreads => Math.Max(1, Environment.ProcessorCount / 2);

    public static ResourceRequirement ForVideoOutput(VideoOutputPlan video, GpuAccelPlan? gpuAccel)
    {
        if (IsGpuEncoder(video.EncoderName))
            return new(
                video.EncoderName,
                GpuSlots: 1,
                CpuThreads: GpuEncodeThreads(video, gpuAccel)
            );

        return new(null, GpuSlots: 0, CpuThreads: SoftwareEncodeThreads);
    }

    private static int GpuEncodeThreads(VideoOutputPlan video, GpuAccelPlan? gpuAccel)
    {
        // Decode and scale are GPU-resident — only ffmpeg's demux/mux and
        // NVENC's own helper threads touch the CPU.
        if (gpuAccel is not null)
            return 2;

        // CPU tonemapping (zscale/tonemap) dominates an HDR→SDR pass and costs
        // as much as a software encode of the same rung; the encoder being
        // nvenc buys nothing here.
        if (video.ConvertHdrToSdr || video.TonemapFilterChain is not null)
            return Math.Max(2, SoftwareEncodeThreads);

        // CPU decode plus the CPU filter graph, feeding a GPU encoder.
        return Math.Max(2, Environment.ProcessorCount / 4);
    }

    public static ResourceRequirement CpuOnly(int cpuThreads = 1) =>
        new(null, GpuSlots: 0, CpuThreads: cpuThreads);

    private static bool IsGpuEncoder(string encoderName)
    {
        if (string.IsNullOrEmpty(encoderName))
            return false;

        string lower = encoderName.ToLowerInvariant();
        foreach (string token in GpuEncoderTokens)
        {
            if (lower.Contains(token, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
