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

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// The quality knob a preset picks instead of hand-tuning CRF and speed preset
/// per codec. <see cref="CodecTunings"/> maps each tier onto the settings that
/// mean the same thing for x264, x265, SVT-AV1 and libvpx, whose CRF scales are
/// not comparable to one another.
/// </summary>
public enum EncodingQuality
{
    /// <summary>Near-lossless. Very slow, very large. For a master copy.</summary>
    Archive,

    /// <summary>Visually lossless at normal viewing distance.</summary>
    Ultra,

    VeryHigh,
    High,

    /// <summary>The trade-off to reach for when nothing else is specified.</summary>
    Balanced,

    /// <summary>Tuned for adaptive ladders, where the rung bitrate leads.</summary>
    Streaming,

    Fast,

    /// <summary>Throwaway quality for a quick look at the output.</summary>
    Preview,
}
