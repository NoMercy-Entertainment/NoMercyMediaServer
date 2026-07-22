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

namespace NoMercy.Encoder.Strategies.Shared;

/// <summary>
/// Derives <see cref="ResourceRequirement"/> for a decomposed task from the
/// output plan. GPU is detected by encoder name suffix — any encoder whose
/// name contains a hardware-acceleration token (nvenc, amf, qsv, vaapi,
/// videotoolbox, cuvid) gets a GPU-slot requirement. All other tasks get
/// CPU-only requirements.
/// </summary>
internal static class TaskResourceHelper
{
    private static readonly IReadOnlyList<string> GpuEncoderTokens = Hardware
        .GpuEncoderTokens
        .VendorPrefixes;

    public static ResourceRequirement ForVideoOutput(VideoOutputPlan video)
    {
        if (IsGpuEncoder(encoderName: video.EncoderName))
            return new(GpuDeviceKey: video.EncoderName, GpuSlots: 1, CpuThreads: 2);

        int cpuThreads = Math.Max(val1: 1, val2: Environment.ProcessorCount / 2);
        return new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: cpuThreads);
    }

    public static ResourceRequirement CpuOnly(int cpuThreads = 1) =>
        new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: cpuThreads);

    private static bool IsGpuEncoder(string encoderName)
    {
        if (string.IsNullOrEmpty(value: encoderName))
            return false;

        string lower = encoderName.ToLowerInvariant();
        foreach (string token in GpuEncoderTokens)
        {
            if (lower.Contains(value: token, comparisonType: StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
