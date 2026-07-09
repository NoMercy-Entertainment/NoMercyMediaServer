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

namespace NoMercyQueue;

/// <summary>
/// Lets a host-specific signal defer a <see cref="Workers.QueueWorker"/> from
/// reserving new jobs for a given queue, without stopping the worker thread
/// or persisting any state. Transient only — checked once per poll iteration,
/// forgotten the moment the condition clears.
///
/// <para>Deliberately dependency-free: this project must not reference media
/// types. Implementations (e.g. one that consults playback activity) live in
/// the host project and are injected the same way <c>IResourceBudget</c> is.</para>
/// </summary>
public interface IWorkerActivityGate
{
    /// <summary>
    /// True when the worker for <paramref name="queueName"/> should defer
    /// reserving its next job.
    /// </summary>
    bool ShouldDefer(string queueName);

    /// <summary>
    /// How long to wait before the worker re-checks <see cref="ShouldDefer"/>.
    /// </summary>
    TimeSpan DeferInterval { get; }
}
