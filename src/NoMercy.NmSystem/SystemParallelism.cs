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

namespace NoMercy.NmSystem;

/// <summary>
/// Host-derived parallelism defaults for fan-out loops (Parallel.ForEach / ForEachAsync).
/// </summary>
/// <remarks>
/// Degree-of-parallelism is capped at half the logical processors so background
/// scans and imports never saturate the box the user is also watching media on.
/// This is a derived runtime constant, not user configuration — hence a plain
/// static value rather than an injected option.
/// </remarks>
public static class SystemParallelism
{
    public static readonly ParallelOptions Options = new()
    {
        MaxDegreeOfParallelism = (int)Math.Floor(d: Environment.ProcessorCount / 2.0),
    };
}
