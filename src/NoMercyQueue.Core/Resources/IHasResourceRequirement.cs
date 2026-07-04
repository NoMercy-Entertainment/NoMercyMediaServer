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

namespace NoMercyQueue.Core.Resources;

/// <summary>
/// Implemented by queue jobs that carry an explicit <see cref="ResourceRequirement"/>.
/// <see cref="QueueWorker"/> reads this before executing the job to gate dispatch
/// against the in-flight resource budget.
/// </summary>
public interface IHasResourceRequirement
{
    ResourceRequirement? ResourceRequirement { get; }
}
