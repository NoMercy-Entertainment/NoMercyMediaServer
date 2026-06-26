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

namespace NoMercy.NmSystem.Lifecycle;

/// <summary>
/// Guards queue workers from pulling jobs until the server is fully started
/// and all registered readiness signals have resolved.
/// </summary>
public interface IServerReadinessGate
{
    /// <summary>
    /// Resolves when the server is fully ready to process queue jobs:
    /// host ApplicationStarted has fired AND all registered readiness
    /// signals have completed.
    /// </summary>
    Task WaitForReadyAsync(CancellationToken ct);

    /// <summary>
    /// Register an additional async signal that must complete before
    /// WaitForReadyAsync resolves. Call from hosted services during
    /// their own StartAsync — never AFTER WaitForReadyAsync has been
    /// awaited.
    /// </summary>
    void AddSignal(string name, Task signal);
}
