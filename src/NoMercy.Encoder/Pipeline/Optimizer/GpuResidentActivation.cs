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

using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Pipeline.Optimizer;

/// <summary>
/// Composes the GPU-resident gate: opt-in flag, GPU presence, eligibility, the
/// no-thumbnails constraint (sprites need a CPU download), and the vendor scaler
/// being present in the running ffmpeg build. Returns the resolved
/// <see cref="GpuAccelPlan"/> or null for the CPU path. Pure so the whole
/// decision is unit-tested without a full PlanStage rig.
/// </summary>
public static class GpuResidentActivation
{
    public static GpuAccelPlan? Resolve(
        bool enabled,
        bool hasGpu,
        GpuVendor? vendor,
        OutputPlan plan,
        Func<string, bool> hasFilter
    )
    {
        if (!enabled || !hasGpu)
            return null;
        if (plan.Thumbnails is not null)
            return null;
        if (!GpuResidentEligibility.IsEligible(plan.VideoOutputs, plan.SubtitleOutputs))
            return null;

        return GpuAccelResolver.Resolve(vendor, hasFilter);
    }
}
