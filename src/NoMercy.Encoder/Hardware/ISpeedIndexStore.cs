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

public interface ISpeedIndexStore
{
    SpeedIndex? Load();
    void Save(SpeedIndex index);
    DateTime? LastCalibratedAt { get; }

    /// <summary>
    /// Schema version of the most recently loaded cache file. Null when no
    /// cache has been loaded yet. Compared against the current
    /// <see cref="HardwareBenchmark.BenchmarkSchemaVersion"/> in
    /// <see cref="HardwareBenchmark.NeedsRecalibration"/> so a code change
    /// to probe length, tier list, or candidate enumeration auto-invalidates
    /// stale cached numbers without waiting for the 30-day grace window.
    /// </summary>
    int? LoadedSchemaVersion { get; }
}
