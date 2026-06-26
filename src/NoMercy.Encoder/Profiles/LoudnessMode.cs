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
/// Loudness normalisation mode applied to an audio output.
/// </summary>
public enum LoudnessMode
{
    /// <summary>No normalisation pass — stream is encoded at source loudness.</summary>
    None,

    /// <summary>
    /// EBU R 128 / ITU-R BS.1770-4 normalisation. Two-pass via the loudnorm
    /// filter so the integrated loudness matches the target LUFS.
    /// </summary>
    EbuR128,

    /// <summary>
    /// ReplayGain-style peak normalisation. Faster (single pass) but only
    /// considers peak amplitude, not perceived loudness.
    /// </summary>
    ReplayGain,

    /// <summary>
    /// Use the raw ffmpeg arguments supplied in the profile's loudness
    /// config — escape hatch for non-standard pipelines.
    /// </summary>
    Custom,
}
