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

public enum VideoCodecType
{
    H264,
    H265,
    Vp9,
    Av1,

    /// <summary>
    /// Stream copy (no re-encode). Source video bytes are remuxed into the
    /// output container as-is, preserving the exact codec, bit depth, HDR
    /// metadata, and bitrate. Used by archival presets where the user wants
    /// the original quality with a different container (e.g. source MKV →
    /// MKV with new audio tracks). Skips every encode-time decision: no
    /// hardware encoder picked, no CRF translation, no preset / profile /
    /// level / tune applied, no HDR tonemap, no pixel-format conversion.
    /// Encoder pipeline emits <c>-c:v copy</c>.
    /// </summary>
    Copy,
}
