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
/// A skip-intro / skip-outro boundary, expressed in real-time offsets
/// into the source file. Confidence is 0..1 — the proportion of the
/// matched fingerprint window that was actually similar (not just a
/// low-magnitude false positive).
/// </summary>
/// <param name="FingerprintHash">
/// Optional stable identifier derived from the contributing fingerprints.
/// Computed by the detection caller (e.g. the content-analysis controller)
/// and surfaced in the API response; not produced by the detector itself.
/// </param>
public record IntroMarker(
    TimeSpan Start,
    TimeSpan End,
    double Confidence,
    string? FingerprintHash = null
)
{
    public TimeSpan Duration => End - Start;
}
