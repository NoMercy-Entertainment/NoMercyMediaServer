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
/// Quality scaler for Intel Quick Sync Video encoders: <c>h264_qsv</c>,
/// <c>hevc_qsv</c>, and <c>av1_qsv</c>.
///
/// <para>QSV's CRF/global_quality range is 1–51 — zero is explicitly
/// excluded because passing 0 disables CRF mode entirely and switches the
/// encoder to unconstrained VBR. This scaler enforces a minimum of 1 so
/// the off-by-one bug never reaches ffmpeg.</para>
///
/// <para>Source: Intel Media SDK manual §ICQ mode; ffmpeg QSV encoder
/// documentation (ffmpeg -h encoder=h264_qsv).</para>
/// </summary>
public sealed class QsvQualityScaler : IQualityScaler
{
    private static readonly HashSet<string> _handles = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        "h264_qsv",
        "hevc_qsv",
        "av1_qsv",
    };

    public bool Supports(string encoderHandle) => _handles.Contains(item: encoderHandle);

    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        // QSV global_quality range: 1–51.
        int qsvMin = 1;
        int qsvMax = targetMax > 0 ? targetMax : 51;

        if (sourceMax <= 0)
            return qsvMin;

        int scaled = (int)Math.Round(a: (double)sourceCrf / sourceMax * qsvMax);

        // Off-by-one guard: never emit 0 — it silently disables CRF mode.
        return Math.Clamp(value: scaled, min: qsvMin, max: qsvMax);
    }
}
