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

namespace NoMercy.Encoder.Pipeline.Optimizer;

/// <summary>
/// Decides whether a job should produce one near-lossless full-res SDR mezzanine
/// and derive its rungs from it, versus the fused/sequential path. A mezzanine
/// only pays for its extra full-res write when the work is both heavy AND spread
/// across workers — on a single box the sequential bundles are cheaper. The first
/// real consumer of <see cref="IEncodeCostModel.TotalCost"/>.
/// </summary>
public static class MezzaninePlanner
{
    public static bool ShouldUseMezzanine(
        double totalCost,
        bool distributedEncodingEnabled,
        int workerCount,
        int rungCount,
        double threshold
    )
    {
        if (!distributedEncodingEnabled)
            return false;
        if (workerCount < 2)
            return false;
        // A mezzanine amortises the shared decode+tonemap across many derived
        // rungs; with one rung there is nothing to amortise.
        if (rungCount < 2)
            return false;

        return totalCost >= threshold;
    }
}
