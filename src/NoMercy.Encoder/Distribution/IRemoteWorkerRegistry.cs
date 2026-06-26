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

using NoMercy.Encoder.Jobs;

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Source of truth for which remote workers the server currently believes it
/// can send encode tasks to. Empty in standalone installs. Populated by a
/// future network service (SignalR hub / heartbeat protocol). The dispatcher
/// reads this registry once per dispatch — no streaming, no subscription.
/// </summary>
public interface IRemoteWorkerRegistry
{
    IReadOnlyList<IRemoteWorker> GetActiveWorkers();
}

/// <summary>
/// Default empty registry — keeps the dispatcher DI graph satisfied for
/// installs that don't run distributed encoding. Plugins or a future
/// SignalR-backed implementation replace this in DI.
/// </summary>
public sealed class EmptyRemoteWorkerRegistry : IRemoteWorkerRegistry
{
    public IReadOnlyList<IRemoteWorker> GetActiveWorkers() => [];
}
