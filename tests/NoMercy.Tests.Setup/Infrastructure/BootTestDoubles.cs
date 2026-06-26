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

using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.Setup.Infrastructure;

/// <summary>No-op <see cref="IApiKeyLoader"/> for boot tests that do not exercise key loading.</summary>
public sealed class FakeApiKeyLoader : IApiKeyLoader
{
    public Task LoadKeys(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>No-op <see cref="IServerRegistrationService"/> for boot/setup tests.</summary>
public sealed class FakeServerRegistrationService : IServerRegistrationService
{
    public Task Init(int maxRetries = 5) => Task.CompletedTask;

    public Task GetTunnelAvailability() => Task.CompletedTask;
}

/// <summary>No-op <see cref="IDegradedModeRecovery"/> for boot tests.</summary>
public sealed class FakeDegradedModeRecovery : IDegradedModeRecovery
{
    public Task StartRecoveryLoop(DeferredTasks tasks) => Task.CompletedTask;
}
