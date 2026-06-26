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

using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Extension methods for <see cref="StageResult"/> that produce the
/// dashboard-facing <see cref="PlanResult"/> without changing the
/// <see cref="PlanStage.ExecuteAsync"/> return type. The orchestrator
/// and future API controllers call <see cref="AsPlanResult"/> when they
/// need the spec shape; all existing consumers of
/// <see cref="StageSuccess{T}"/> are unaffected.
/// </summary>
public static class PlanStageExtensions
{
    /// <summary>
    /// Returns a <see cref="PlanResult"/> when <paramref name="result"/>
    /// is a <see cref="StageSuccess{ExecutionPlan}"/>; otherwise null.
    /// </summary>
    public static PlanResult? AsPlanResult(this StageResult result, EncodingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (result is StageSuccess<ExecutionPlan> success)
            return PlanResultProjector.FromExecutionPlan(success.Value, context);

        return null;
    }
}
