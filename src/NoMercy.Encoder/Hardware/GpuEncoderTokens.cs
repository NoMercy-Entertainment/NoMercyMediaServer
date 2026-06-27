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
}
