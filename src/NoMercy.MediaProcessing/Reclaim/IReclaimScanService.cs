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

namespace NoMercy.MediaProcessing.Reclaim;

public interface IReclaimScanService
{
    ReclaimScanState State { get; }

    DateTimeOffset? LastScannedAt { get; }

    ReclaimScanResult? Latest { get; }

    Task StartScanAsync(CancellationToken ct);

    Task<long> DeleteItemAsync(string itemId, CancellationToken ct);

    Task<(int count, long bytes)> SweepPartialsAsync(CancellationToken ct);
}
