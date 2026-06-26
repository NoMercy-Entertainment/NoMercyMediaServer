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

public enum AudioCodecType
{
    Aac,
    Flac,
    Opus,
    Ac3,
    Eac3,
    Mp3,
    Vorbis,
    TrueHd,
    Dts,

    /// <summary>
    /// Audio stream copy (no re-encode). Used in archival presets to preserve
    /// lossless / multi-channel source tracks (TrueHD, DTS-HD MA, FLAC) in
    /// place of a lossy re-encode. Bitrate / channels / sample-rate fields
    /// on the AudioOutput are ignored — the copied bytes carry their own
    /// values. Encoder pipeline emits <c>-c:a copy</c>.
    /// </summary>
    Copy,
}
