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

namespace NoMercy.Encoder.Distribution;

public interface IWorkerDispatcher
{
    /// <summary>
    /// Runs <paramref name="tasks"/> across available workers. The dispatcher
    /// decides whether to run locally, split across remote workers, or a mix.
    /// Returns one <see cref="DispatchResult"/> per input task in the same
    /// order as <paramref name="tasks"/>. A partial failure reports the
    /// specific task's success=false; callers decide whether to retry or
    /// abort the surrounding encode.
    /// </summary>
    Task<DispatchResult[]> DispatchAsync(EncodeTask[] tasks, CancellationToken ct);

    /// <summary>Number of workers available to this dispatcher right now.</summary>
    int AvailableWorkerCount { get; }
}
