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
    /// </summary>
    public static int GpuEncodeWithCpuFilters => Math.Max(2, Environment.ProcessorCount / 4);

    /// <summary>
    /// A hardware encode whose decode and scaling are GPU-resident: only
    /// ffmpeg's demux/mux and the encoder's own helper threads touch the CPU.
    /// </summary>
    public const int GpuResidentEncode = 2;
}
