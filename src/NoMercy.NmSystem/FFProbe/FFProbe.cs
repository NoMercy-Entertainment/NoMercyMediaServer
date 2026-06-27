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

using NoMercy.Storage;

namespace NoMercy.NmSystem.FFProbe;

// Thin static facade over the injectable FfProbeService for ambient call sites.
// Prefer injecting IFfProbeService where a DI scope is available.
public static class FfProbe
{
    public static Task<FfProbeData> CreateAsync(string file, CancellationToken ct = default) =>
        FfProbeService.Current.CreateAsync(file, ct);

    public static Task<FfProbeData> CreateAsync(
        IStorageDriver driver,
        string file,
        CancellationToken ct = default
    ) => FfProbeService.Current.CreateAsync(driver, file, ct);
}
