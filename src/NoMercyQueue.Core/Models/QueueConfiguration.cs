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

namespace NoMercyQueue.Core.Models;

public record QueueConfiguration
{
    public Dictionary<string, int> WorkerCounts { get; init; } = new();

    public byte MaxAttempts { get; init; } = 3;
    public int PollingIntervalMs { get; init; } = 1000;
}
