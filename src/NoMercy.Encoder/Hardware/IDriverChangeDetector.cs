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

namespace NoMercy.Encoder.Hardware;

public record DriverChangeResult(
    string CurrentHash,
    string? PreviousHash,
    bool Changed,
    bool IsFirstBoot
);

public interface IDriverChangeDetector
{
    /// <summary>
    /// Builds a fingerprint from the current GPU set, compares it to the previously
    /// persisted hash, saves the new hash, and returns the comparison result.
    /// </summary>
    Task<DriverChangeResult> DetectAndPersistAsync(CancellationToken ct = default);
}
