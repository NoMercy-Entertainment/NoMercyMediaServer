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
/// Resolves how many encoder streams should sit inside ONE ffmpeg
/// invocation before we split into additional bundles that the queue
/// worker pulls sequentially. The cap is the practical throughput ceiling,
/// not the driver-allowed maximum.
///
/// <para><b>Signal-driven, not model-string-driven.</b> The hardware
/// benchmark (<see cref="IHardwareBenchmark"/>) measures real fps per
/// (codec × encoder × width × device) on this exact host. The cap is
/// derived from those measurements + a headroom factor so every stream
/// in the bundle keeps a target realtime multiplier even when sharing
/// the encoder block.</para>
///
/// <para>Math: if a single rung at the bundle's heaviest resolution
/// benchmarks at <c>S</c> times realtime in isolation, N parallel
/// instances each get roughly <c>S/N</c> on the same engine. To keep each
/// stream at <see cref="TargetRealtimeMultiplier"/>, the cap is
/// <c>floor(S / target)</c>.</para>
///
/// <para>Falls back to conservative defaults when benchmark data isn't
/// available for the planned rungs (fresh server / new encoder / GPU
/// driver upgrade). Driver-imposed session caps from
/// <see cref="GpuDevice.MaxEncoderSessions"/> still apply as an outer
/// ceiling so we never propose more sessions than the driver allows.</para>
/// </summary>
public static class BundleCapResolver
{
    /// <summary>
    /// Target realtime multiplier each stream should retain when bundled.
    /// 1.5× means a 30-fps source still encodes at ≥45 fps per stream when
    /// sharing the engine — enough headroom that the host doesn't fall
    /// behind real-time on any single bundle.
    /// </summary>
    private const double TargetRealtimeMultiplier = 1.5;

    /// <summary>
    /// Conservative fallback used when no benchmark measurement is
    /// available for any of the planned GPU rungs. Two is a sensible "I
    /// don't know this hardware" number — bigger than 1 so we still bundle
    /// at all, small enough that a slow box doesn't get pinned.
    /// </summary>
    private const int UnknownGpuCapFallback = 2;

    private const int UnknownCpuCapFallback = 1;

    /// <summary>
    /// One planned rung the bundler can score against the benchmark.
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
    /// <param name="benchmark">Hardware benchmark (cached SpeedIndex).</param>
    /// <param name="hardware">Detected hardware capabilities.</param>
    /// <returns>
    /// <c>GpuCap</c> = max GPU streams per ffmpeg; <c>CpuCap</c> = max
    /// software streams per ffmpeg. Both capped at any driver-imposed
    /// session limit reported by <see cref="GpuDevice.MaxEncoderSessions"/>.
    /// </returns>
    public static (int GpuCap, int CpuCap) Resolve(
        IReadOnlyList<PlannedRung> rungs,
        IHardwareBenchmark? benchmark,
        IHardwareCapabilities? hardware
    )
    {
        SpeedIndex? speed = benchmark?.GetCachedIndex();
        GpuDevice? gpu = hardware is { HasGpu: true, Gpus.Count: > 0 } ? hardware.Gpus[0] : null;

        int gpuCap = ResolveGpuCap(rungs, speed, gpu);
        int cpuCap = ResolveCpuCap(rungs, speed);

        // Driver-enforced ceiling — never propose more concurrent NVENC
        // sessions than the silicon/driver allows even if benchmark
        // suggests otherwise.
        if (gpu is { MaxEncoderSessions: > 0 and not int.MaxValue })
            gpuCap = Math.Min(gpuCap, gpu.MaxEncoderSessions);

        return (Math.Max(1, gpuCap), Math.Max(1, cpuCap));
    }

    private static int ResolveGpuCap(
        IReadOnlyList<PlannedRung> rungs,
        SpeedIndex? speed,
        GpuDevice? gpu
    )
    {
        if (speed is null || gpu is null)
            return UnknownGpuCapFallback;

        // Pick the slowest GPU rung in the plan — it limits the bundle
        // because once it saturates the engine, the rest don't get
        // proportional time either.
        double slowest = double.MaxValue;
        bool foundAny = false;
        foreach (PlannedRung rung in rungs)
        {
            if (!rung.IsGpu)
                continue;

            SpeedMeasurement? measurement = speed.GetSpeed(
                rung.Codec,
                rung.EncoderName,
                rung.Width,
                gpu.Name
            );
            if (measurement is null)
                continue;

            foundAny = true;
            if (measurement.SpeedMultiplier < slowest)
                slowest = measurement.SpeedMultiplier;
        }

        if (!foundAny)
            return UnknownGpuCapFallback;

        return Math.Max(1, (int)Math.Floor(slowest / TargetRealtimeMultiplier));
    }

    private static int ResolveCpuCap(IReadOnlyList<PlannedRung> rungs, SpeedIndex? speed)
    {
        if (speed is null)
            return UnknownCpuCapFallback;

        double slowest = double.MaxValue;
        bool foundAny = false;
        foreach (PlannedRung rung in rungs)
        {
            if (rung.IsGpu)
                continue;

            // Software encoders have a null device key in SpeedIndex.
            SpeedMeasurement? measurement = speed.GetSpeed(
                rung.Codec,
                rung.EncoderName,
                rung.Width,
                null
            );
            if (measurement is null)
                continue;

            foundAny = true;
            if (measurement.SpeedMultiplier < slowest)
                slowest = measurement.SpeedMultiplier;
        }

        if (!foundAny)
            return UnknownCpuCapFallback;

        return Math.Max(1, (int)Math.Floor(slowest / TargetRealtimeMultiplier));
    }
}
