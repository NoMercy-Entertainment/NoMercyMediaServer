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
/// Quality scaler for Apple VideoToolbox encoders: <c>h264_videotoolbox</c>
/// and <c>hevc_videotoolbox</c>.
///
/// <para>VideoToolbox uses <c>-q:v 0–100</c> where <b>higher means better
/// quality</b> — the opposite of CRF (where lower is better). A CRF of 0
/// (lossless) maps to q:v 100, and CRF 51 (lowest quality) maps to q:v 0.
/// This scaler inverts the linear scale to preserve intent.</para>
///
/// <para>Source: Apple WWDC 2022 "Encoding and streaming media" session;
/// ffmpeg VideoToolbox encoder docs (ffmpeg -h encoder=h264_videotoolbox).</para>
/// </summary>
public sealed class VideoToolboxQualityScaler : IQualityScaler
{
    private static readonly HashSet<string> _handles = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        "h264_videotoolbox",
        "hevc_videotoolbox",
    };

    public bool Supports(string encoderHandle) => _handles.Contains(item: encoderHandle);

    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        // targetMax for VideoToolbox is always 100 (q:v 0–100).
        int vtMax = targetMax > 0 ? targetMax : 100;

        if (sourceMax <= 0)
            return vtMax; // Unknown scale → default to best quality

        int linearScaled = (int)Math.Round(a: (double)sourceCrf / sourceMax * vtMax);
        int inverted = vtMax - linearScaled;

        // Inversion: CRF 0 (best quality) → q:v 100 (best quality).
        return Math.Clamp(value: inverted, min: 0, max: vtMax);
    }
}
