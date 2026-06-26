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
/// Quality scaler for AMD AMF AV1 encoder: <c>av1_amf</c>.
///
/// <para>AMD's AV1 AMF encoder uses QP 0–255 — a full 8-bit range that is
/// NOT the 0–51 range used by AMF H.264 or AMF HEVC. Passing a raw CRF
/// value of, say, 30 without scaling would be near-lossless (30/255) when
/// the user intended a mid-quality AV1 encode. This scaler maps the
/// source's 0–63 AV1 CRF scale linearly to 0–255.</para>
///
/// <para>Source: AMD AMF SDK AV1 encoder documentation, §QP mode;
/// Av1Definition.cs QualityRange(Min: 0, Max: 255, Default: 100).</para>
/// </summary>
public sealed class AmfAv1QualityScaler : IQualityScaler
{
    public bool Supports(string encoderHandle) =>
        string.Equals(encoderHandle, "av1_amf", StringComparison.OrdinalIgnoreCase);

    public int Translate(int sourceCrf, int sourceMax, int targetMax, CodecHint hint)
    {
        // AMF AV1 QP range: 0–255.
        int amfMax = targetMax > 0 ? targetMax : 255;

        if (sourceMax <= 0)
            return 0;

        int scaled = (int)Math.Round((double)sourceCrf / sourceMax * amfMax);
        return Math.Clamp(scaled, 0, amfMax);
    }
}
