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
namespace NoMercy.Providers.AcoustId;

/// <summary>
/// Audio fingerprinting seam. The concrete chromaprint-backed implementation is
/// delivered in Slice 14; domain services depend on this abstraction so they can
/// request fingerprints without binding to the fpcalc/FFmpeg integration.
/// </summary>
public interface IAudioFingerprinter
{
    Task<AudioFingerprint?> FingerprintAsync(string filePath, CancellationToken ct);
}

public record AudioFingerprint(string Fingerprint, int DurationSeconds);
