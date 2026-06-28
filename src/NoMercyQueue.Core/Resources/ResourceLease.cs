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
/// An active hold on hardware resources granted by <see cref="IResourceBudget"/>.
/// Release by calling <see cref="IResourceBudget.Release"/> (or dispose the budget
/// wrapper if one is provided). Leases are value types — they must not outlive the
/// <see cref="IResourceBudget"/> that issued them.
/// </summary>
public record ResourceLease(string LeaseId, string? GpuDeviceKey, int GpuSlots, int CpuThreads);
