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
/// Projects an <see cref="ExecutionPlan"/> into the dashboard-facing
/// <see cref="PlanResult"/> spec-shape. Injectable so it can be mocked in tests
/// or replaced by a plugin.
/// </summary>
public interface IPlanResultProjector
{
    /// <summary>
    /// Build a <see cref="PlanResult"/> from an <see cref="ExecutionPlan"/>
    /// and the current <see cref="EncodingContext"/>.
    /// </summary>
    PlanResult FromExecutionPlan(ExecutionPlan plan, EncodingContext context);
}
