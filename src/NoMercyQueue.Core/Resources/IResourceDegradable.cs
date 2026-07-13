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

using NoMercyQueue.Core.Interfaces;

namespace NoMercyQueue.Core.Resources;

/// <summary>
/// Implemented by queue jobs whose <see cref="ResourceRequirement"/> can be
/// safely re-planned onto software/CPU when its GPU device is permanently
/// absent. The queue worker's budget gate calls this when
/// <see cref="IResourceBudget.IsGpuDeviceRegistered"/> reports the job's
/// <see cref="ResourceRequirement.GpuDeviceKey"/> as unregistered — a GPU
/// pinned to hardware that does not exist on this host can never be granted
/// a slot, so it must degrade instead of looping at the budget gate forever.
/// </summary>
public interface IResourceDegradable
{
    /// <summary>
    /// Returns a copy of this job re-planned to run without its GPU
    /// requirement (CPU/software), routed via the returned job's own
    /// <see cref="IShouldQueue.QueueName"/>. Returns null when this job has
    /// no software fallback — the caller keeps the original behavior
    /// (release and retry) rather than silently dropping work it cannot
    /// confidently degrade.
    /// </summary>
    IShouldQueue? DegradeToSoftware();
}
