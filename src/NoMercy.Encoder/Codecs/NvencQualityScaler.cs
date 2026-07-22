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

namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Quality scaler for NVIDIA NVENC encoders: <c>h264_nvenc</c>,
/// <c>hevc_nvenc</c>, and <c>av1_nvenc</c>.
///
/// <para>NVENC exposes CQ (Constant Quality) mode. For H.264 and HEVC the
/// CQ range matches libx264/x265 CRF: 0–51, so values pass through
/// 1:1. For AV1 the source profile is written in 0–63 terms (SVT-AV1 /
/// libaom) but av1_nvenc's CQ is still 0–51, so the value is scaled
/// proportionally.</para>
///
/// <para>Source: NVIDIA Video Codec SDK programming guide, §CQ mode;
/// ffmpeg NVENC options (ffmpeg -h encoder=av1_nvenc).</para>
/// </summary>
public sealed class NvencQualityScaler : IQualityScaler
{
    private static readonly HashSet<string> _handles = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        "h264_nvenc",
        "hevc_nvenc",
        "av1_nvenc",
    };

    public bool Supports(string encoderHandle) => _handles.Contains(item: encoderHandle);

    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        // NVENC CQ is always 0–51 regardless of codec.
        // When the source is already on a 0–51 scale (H.264 / HEVC), pass through.
        // When the source is on a 0–63 scale (AV1 profile), scale proportionally.
        int nvencMax = 51;

        if (sourceMax == nvencMax)
            return Math.Clamp(value: sourceCrf, min: 0, max: nvencMax);

        int scaled = (int)Math.Round(a: (double)sourceCrf / sourceMax * nvencMax);
        return Math.Clamp(value: scaled, min: 0, max: nvencMax);
    }
}
