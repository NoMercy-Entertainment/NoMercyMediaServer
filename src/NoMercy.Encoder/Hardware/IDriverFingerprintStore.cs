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

public interface IDriverFingerprintStore
{
    /// <summary>
    /// Loads the previously persisted driver fingerprint hash.
    /// Returns null when the file is missing or corrupt.
    /// </summary>
    Task<string?> LoadHashAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the current driver fingerprint hash to durable storage.
    /// </summary>
    Task SaveHashAsync(string hash, CancellationToken ct = default);
}
