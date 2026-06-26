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

namespace NoMercy.Encoder.ContentAnalysis.Fingerprinting;

/// <summary>
/// Finds shared fingerprint regions between episodes of the same season —
/// typically the opening theme and the end-credits music. The detector is
/// pure / stateless: callers supply two or more pre-computed fingerprints
/// from episodes and get back the overlap region expressed in source-file
/// offsets.
///
/// "Shared" is defined as a contiguous run of frames where the Hamming
/// distance between each pair of hashes stays below a threshold. Empty
/// or mismatched fingerprints yield <c>null</c> rather than zero-length
/// markers so callers can distinguish "no intro" from "failed to detect".
/// </summary>
public interface IIntroDetector
{
    /// <summary>
    /// Finds the longest shared leading region across all supplied
    /// fingerprints — the intro. Callers should fingerprint the first few
    /// minutes of each episode (intros never run longer than ~3 min).
    /// </summary>
    IntroMarker? DetectIntro(IReadOnlyList<AudioFingerprint> episodeFingerprints);

    /// <summary>
    /// Same matching logic as <see cref="DetectIntro"/> but applied to
    /// fingerprints taken from the tail of each episode — finds outros.
    /// </summary>
    IntroMarker? DetectOutro(IReadOnlyList<AudioFingerprint> episodeFingerprints);
}
