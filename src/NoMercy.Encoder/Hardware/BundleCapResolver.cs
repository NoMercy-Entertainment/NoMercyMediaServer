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

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Resolves how many encoder streams may share ONE ffmpeg invocation before
/// the planner is forced to split into additional bundles that the queue
/// worker pulls sequentially.
///
/// <para><b>The cap is a hard concurrency limit, not a throughput knob.</b>
/// Every rung the planner hands to one bundle shares a single physical decode
/// (and, when tonemapping, a single HDR→SDR pass): <c>FilterGraphAssembler</c>
/// hoists the source crop once, before branching, so an HDR-preserve 4K master
/// and the SDR rung derived from it come out of ONE ffmpeg, ONE decode. That
/// is the whole point of the decode-aware planner. Splitting those rungs into
/// separate ffmpegs does NOT make them encode faster — the encoder silicon is
/// shared across processes regardless — it only duplicates the expensive
/// decode + crop + tonemap. So the ONLY legitimate reason to split a shared
/// decode is running out of concurrent encoder sessions.</para>
///
/// <para><b>GPU:</b> the cap is the driver-reported concurrent NVENC session
/// limit (<see cref="GpuDevice.MaxEncoderSessions"/>) — an ffmpeg that opens
/// more sessions than the driver allows fails outright, so this is a
/// correctness bound, not a preference. Consumer cards report a small finite
/// number (e.g. 8); professional / patched-driver cards report
/// <see cref="int.MaxValue"/>, in which case a practical ceiling keeps a single
/// bundle from growing unbounded while still holding every realistic rendition
/// ladder together.</para>
///
/// <para><b>CPU:</b> software encoders have no session limit; the bound is
/// simply how many can each still get a viable thread slice off the host's
/// cores. The bundle's total thread demand is separately clamped to
/// <c>cores - 1</c> in <c>DecodeAwareBundlePlanner.ResolveBundleResources</c>.</para>
///
/// <para>Host overload from running too much AT ONCE is a different concern,
/// owned by the GPU/CPU semaphores in <c>ResourceBudget</c> and the per-bundle
/// resource claim — never by fragmenting a single job's shared decode.</para>
/// </summary>
public static class BundleCapResolver
{
    /// <summary>
    /// Practical per-bundle ceiling when the GPU reports no finite session
    /// limit (professional / patched-driver cards → <see cref="int.MaxValue"/>).
    /// Large enough that every real rendition ladder off one source stays in a
    /// single ffmpeg, small enough to avoid a pathological hundred-session
    /// invocation if a plan ever ballooned.
    /// </summary>
    private const int UnlimitedGpuBundleCap = 32;

    /// <summary>
    /// Minimum threads a software encode needs to make progress. Caps how many
    /// CPU rungs share one decode before threads are spread too thin — the
    /// bundle-wide thread demand is clamped again downstream.
    /// </summary>
    private const int MinThreadsPerSoftwareEncode = 2;

    /// <summary>
    /// Conservative CPU cap when core count is unknown.
    /// </summary>
    private const int UnknownCpuCapFallback = 1;

    /// <summary>
    /// One planned rung. Retained for call-site compatibility; only
    /// <see cref="IsGpu"/> influences the cap now that the split is driven by
    /// hardware session capacity rather than per-rung benchmark throughput.
    /// </summary>
    public readonly record struct PlannedRung(
        VideoCodecType Codec,
        string EncoderName,
        int Width,
        bool IsGpu
    );

    /// <summary>
    /// Compute the per-bundle stream caps for GPU and CPU video tasks.
    /// </summary>
    /// <param name="rungs">Every video rung in the encode plan.</param>
    /// <param name="hardware">Detected hardware capabilities.</param>
    /// <returns>
    /// <c>GpuCap</c> = max GPU encode sessions that may share one ffmpeg (the
    /// driver session limit); <c>CpuCap</c> = max software encodes that may
    /// share one ffmpeg (core-bounded).
    /// </returns>
    public static (int GpuCap, int CpuCap) Resolve(
        IReadOnlyList<PlannedRung> rungs,
        IHardwareCapabilities? hardware
    )
    {
        GpuDevice? gpu = hardware is { HasGpu: true, Gpus.Count: > 0 } ? hardware.Gpus[0] : null;

        int gpuCap = ResolveGpuCap(gpu);
        int cpuCap = ResolveCpuCap(hardware);

        return (Math.Max(1, gpuCap), Math.Max(1, cpuCap));
    }

    private static int ResolveGpuCap(GpuDevice? gpu)
    {
        if (gpu is null || gpu.MaxEncoderSessions <= 0 || gpu.MaxEncoderSessions == int.MaxValue)
            return UnlimitedGpuBundleCap;

        return gpu.MaxEncoderSessions;
    }

    private static int ResolveCpuCap(IHardwareCapabilities? hardware)
    {
        int cores = hardware?.CpuCores ?? 0;
        if (cores <= 0)
            return UnknownCpuCapFallback;

        return Math.Max(1, cores / MinThreadsPerSoftwareEncode);
    }
}
