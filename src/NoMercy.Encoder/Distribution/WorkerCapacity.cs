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

/// <summary>
/// Snapshot of a worker's current throughput potential used by the
/// <see cref="IWorkerAssigner"/> to weight task distribution. Higher
/// <see cref="SpeedMultiplier"/> = faster box at this specific tier;
/// higher <see cref="AvailableSlots"/> = more room for parallel work
/// before the worker tips over.
/// </summary>
public record WorkerCapacity(
    string WorkerId,
    double SpeedMultiplier,
    int AvailableSlots,
    /// <summary>
    /// True when the worker has at least one GPU encoder slot available.
    /// The assigner uses this to route <c>EncodeTask.RequiresGpu</c> tasks
    /// to GPU-equipped workers and keep CPU-only workers focused on
    /// CPU-only tasks. Defaults to false so existing single-machine /
    /// CPU-only setups keep working without explicit migration.
    /// </summary>
    bool HasGpu = false
);
