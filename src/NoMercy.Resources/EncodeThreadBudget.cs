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

namespace NoMercy.Resources;

/// <summary>
/// How many CPU threads one encode of a given shape actually consumes. Every
/// producer of a video <c>ResourceRequirement</c> reads its number from here —
/// the task decomposer, the live-transcode runner, and the GPU→software
/// degrade path — so a run cannot reserve one share while another part of the
/// system assumes a different one.
///
/// A GPU slot covers the encode only. Unless decode and scaling are also
/// GPU-resident, ffmpeg still decodes and filters every frame on the CPU, and
/// under-reporting that is what lets the scheduler start a software encode
/// beside a hardware one and peg the host.
/// </summary>
public static class EncodeThreadBudget
{
    /// <summary>A software encode: the dominant CPU consumer on the box.</summary>
    public static int SoftwareEncode => Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    /// A hardware encode fed by a CPU filter graph — CPU decode plus scaling,
    /// GPU encode. Measurably around a fifth of a desktop box per 1080p rung.
    /// Floored at one more than <see cref="GpuResidentEncode"/>, not just 2: on
    /// any host with 11 or fewer cores a floor of 2 collapses to the same
    /// reservation as a fully GPU-resident encode, which is exactly the
    /// under-reporting this class exists to prevent.
    /// </summary>
    public static int GpuEncodeWithCpuFilters =>
        Math.Max(GpuResidentEncode + 1, Environment.ProcessorCount / 4);

    /// <summary>
    /// A hardware encode whose decode and scaling are GPU-resident: only
    /// ffmpeg's demux/mux and the encoder's own helper threads touch the CPU.
    /// </summary>
    public const int GpuResidentEncode = 2;

    /// <summary>
    /// A standalone decode-only pass (subtitle OCR, thumbnail sprite refresh) run
    /// outside the main encode command, so it never inherits the plan stage's
    /// budget-derived <c>-threads</c> flag. Left uncapped, ffmpeg's default thread
    /// pool scales to every logical core, and several such passes running
    /// concurrently (nothing else gates them) can peg the host on their own.
    /// </summary>
    public const int AuxiliaryPass = 2;

    /// <summary>
    /// A thumbnail sprite refresh (decode + fps decimation + scale + pad).
    /// <para>The 6-7 real cores this used to be budgeted for were not swscale
    /// ignoring the flags, as the earlier note here claimed: <c>-threads</c> sat
    /// on the output, where it reaches the encoder and never the decoder, so
    /// h264 decode ran on every core — 10.6 measured on a 41-minute 1080p
    /// episode. Both sprite paths now pass it globally, ahead of the input, and
    /// the same pass measures 1.90 cores.</para>
    /// <para>Budgeted above the measured cost, not at it: the live CPU-headroom
    /// gate is a point-in-time sample that can grant a job before an
    /// already-running one's ramp-up is reflected, so the hard semaphore cap
    /// needs to bound concurrency on its own.</para>
    /// </summary>
    public const int ThumbnailSpriteRefresh = 4;
}
