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

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Single source of truth for GPU encoder token strings, so adding a new GPU
/// encoder is a one-file change instead of an error-prone multi-site edit.
/// </summary>
public static class GpuEncoderTokens
{
    /// <summary>FfmpegName values for NVIDIA NVENC encoders.</summary>
    public static readonly IReadOnlyList<string> NvencNames =
    [
        "h264_nvenc",
        "hevc_nvenc",
        "av1_nvenc",
    ];

    /// <summary>FfmpegName values for AMD AMF encoders.</summary>
    public static readonly IReadOnlyList<string> AmfNames = ["h264_amf", "hevc_amf", "av1_amf"];

    /// <summary>FfmpegName values for Intel Quick Sync (QSV) encoders.</summary>
    public static readonly IReadOnlyList<string> QsvNames =
    [
        "h264_qsv",
        "hevc_qsv",
        "av1_qsv",
        "vp9_qsv",
    ];

    /// <summary>FfmpegName values for VA-API encoders.</summary>
    public static readonly IReadOnlyList<string> VaapiNames =
    [
        "h264_vaapi",
        "hevc_vaapi",
        "av1_vaapi",
        "vp9_vaapi",
    ];

    /// <summary>FfmpegName values for Apple VideoToolbox encoders.</summary>
    public static readonly IReadOnlyList<string> VideotoolboxNames =
    [
        "h264_videotoolbox",
        "hevc_videotoolbox",
    ];

    /// <summary>Hardware-acceleration vendor tokens that mark an encoder/GPU as GPU-backed.</summary>
    public static readonly IReadOnlyList<string> VendorPrefixes =
    [
        "nvenc",
        "amf",
        "qsv",
        "vaapi",
        "videotoolbox",
        "cuvid",
    ];

    /// <summary>
    /// Maps an ffmpeg hardware-encoder name to the GPU vendor required to run
    /// it. Returns null for a software encoder (or any name outside the
    /// vendor lists above) — callers should treat null as "not vendor-gated".
    ///
    /// This is the selection-time counterpart to <see cref="EncoderNamesForVendor"/>
    /// in <c>ResourceBudget</c>: an encoder name is only a real, runnable
    /// option when a physically detected GPU of the matching vendor exists —
    /// ffmpeg's binary capability list alone (which encoders the build was
    /// compiled with) says nothing about which GPUs are actually installed.
    /// </summary>
    public static GpuVendor? VendorForEncoderName(string ffmpegEncoderName)
    {
        if (NvencNames.Contains(ffmpegEncoderName, StringComparer.OrdinalIgnoreCase))
            return GpuVendor.Nvidia;

        if (AmfNames.Contains(ffmpegEncoderName, StringComparer.OrdinalIgnoreCase))
            return GpuVendor.Amd;

        if (
            QsvNames.Contains(ffmpegEncoderName, StringComparer.OrdinalIgnoreCase)
            || VaapiNames.Contains(ffmpegEncoderName, StringComparer.OrdinalIgnoreCase)
        )
            return GpuVendor.Intel;

        if (VideotoolboxNames.Contains(ffmpegEncoderName, StringComparer.OrdinalIgnoreCase))
            return GpuVendor.Apple;

        return null;
    }
}
