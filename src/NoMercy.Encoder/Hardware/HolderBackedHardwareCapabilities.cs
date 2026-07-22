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
/// Singleton-safe forwarding wrapper. Every read goes through to
/// <see cref="HardwareCapabilitiesHolder.Current"/>, so consumers (most
/// importantly <see cref="HardwareBenchmark"/>) pick up the post-detection
/// GPU list even though they were constructed before
/// <see cref="Startup.HardwareInitializationService"/> populated it.
///
/// Without this forwarder the previous factory captured Holder.Current at
/// first resolution — which was always null at startup — and cached an
/// empty CPU-only HardwareCapabilities forever. Net effect: every
/// hardware benchmark ran without GPU encoders, every PreferHardware
/// resolution silently fell back to software, and the dashboard Hardware
/// page never showed any nvenc / qsv / amf rows even on hosts with GPUs.
/// </summary>
public sealed class HolderBackedHardwareCapabilities(HardwareCapabilitiesHolder holder)
    : IHardwareCapabilities
{
    private static readonly IReadOnlySet<string> EmptyUsableHardwareEncoders =
        new HashSet<string>();

    public IReadOnlyList<GpuDevice> Gpus => holder.Current?.Gpus ?? [];

    public int CpuCores => holder.Current?.CpuCores ?? Environment.ProcessorCount;

    public bool HasGpu => holder.Current?.HasGpu ?? false;

    public IReadOnlySet<string> UsableHardwareEncoders =>
        holder.Current?.UsableHardwareEncoders ?? EmptyUsableHardwareEncoders;

    public bool SupportsHardwareEncoding(VideoCodecType codec) =>
        holder.Current?.SupportsHardwareEncoding(codec: codec) ?? false;

    public GpuDevice? GetGpuForCodec(VideoCodecType codec) => holder.Current?.GetGpuForCodec(codec: codec);
}
