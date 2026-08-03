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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Connectivity;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: a strategy that reports IsReady false must be deferred, not judged. The
/// manager must not attempt it, must not report LocalOnly while a bounded deferral window is
/// still open, must establish through it the moment it becomes ready, and — if it never does —
/// must attempt it anyway once the window elapses so a wrong precondition can never
/// permanently hide a transport. This is the exact shape of the real defect: cloudflared not
/// yet downloaded read as "the tunnel failed" instead of "not yet".
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConnectivityManagerReadinessDeferralTests
{
    private sealed class ReadinessControlledStrategy : IConnectivityStrategy
    {
        private volatile bool _isReady;

        public ReadinessControlledStrategy(bool startsReady) => _isReady = startsReady;

        public string Name => "ReadinessControlled";
        public int Priority => 3;
        public ConnectivityType Type => ConnectivityType.CloudflareTunnel;
        public bool IsReady => _isReady;
        public bool WasAttempted { get; private set; }
        public int AttemptCount { get; private set; }
        public bool Succeeds { get; set; } = true;

        public void BecomeReady() => _isReady = true;

        public Task<ConnectivityResult> TryEstablishAsync(CancellationToken ct)
        {
            WasAttempted = true;
            AttemptCount++;
            return Task.FromResult(
                Succeeds ? ConnectivityResult.Verified() : ConnectivityResult.Failed()
            );
        }

        public Task TeardownAsync() => Task.CompletedTask;
    }

    private static ConnectivityManager BuildManager(
        IConnectivityStrategy strategy,
        TimeSpan? delayOverride = null,
        TimeSpan? readinessDeferralWindow = null
    )
    {
        NetworkDiscovery discovery = new(
            NullLogger<NetworkDiscovery>.Instance,
            new Storage.Drivers.Local.LocalStorageDriver(),
            new AuthTokenStore(),
            new ConnectivityStatus(),
            new()
        )
        {
            ExternalIp = "1.2.3.4",
        };

        return new(
            NullLogger<ConnectivityManager>.Instance,
            new AuthTokenStore(),
            discovery,
            [strategy],
            new BootStatus(),
            new ConnectivityStatus(),
            null,
            delayOverride,
            readinessDeferralWindow
        );
    }

    [Fact]
    public async Task EvaluateAsync_StrategyNotReady_IsNeverAttemptedUntilItBecomesReady()
    {
        ReadinessControlledStrategy strategy = new(startsReady: false);
        ConnectivityManager manager = BuildManager(
            strategy,
            delayOverride: TimeSpan.FromMilliseconds(20),
            readinessDeferralWindow: TimeSpan.FromSeconds(30)
        );

        Task evaluate = manager.EvaluateAsync(CancellationToken.None);

        // Give the poll loop several iterations to run while still not ready.
        await Task.Delay(100);
        Assert.False(strategy.WasAttempted);
        Assert.NotEqual(ConnectivityState.LocalOnly, manager.CurrentState);

        strategy.BecomeReady();
        await evaluate;

        Assert.True(strategy.WasAttempted);
        Assert.Equal(1, strategy.AttemptCount);
        Assert.Equal(ConnectivityState.Tunneled, manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_StrategyNeverBecomesReady_AttemptsAnywayAfterWindowElapses()
    {
        ReadinessControlledStrategy strategy = new(startsReady: false) { Succeeds = false };
        ConnectivityManager manager = BuildManager(
            strategy,
            delayOverride: TimeSpan.FromMilliseconds(5),
            readinessDeferralWindow: TimeSpan.FromMilliseconds(50)
        );

        await manager.EvaluateAsync(CancellationToken.None);

        // The window elapsed with the strategy still not ready. It must still be attempted —
        // and that attempt's real (failing) result is what puts the server LocalOnly, not the
        // unresolved precondition itself.
        Assert.True(strategy.WasAttempted);
        Assert.Equal(ConnectivityState.LocalOnly, manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_StrategyReadyFromTheStart_IsAttemptedImmediately_NoAddedLatency()
    {
        ReadinessControlledStrategy strategy = new(startsReady: true);
        ConnectivityManager manager = BuildManager(
            strategy,
            readinessDeferralWindow: TimeSpan.FromMinutes(5)
        );

        DateTime before = DateTime.UtcNow;
        await manager.EvaluateAsync(CancellationToken.None);
        DateTime after = DateTime.UtcNow;

        Assert.True(strategy.WasAttempted);
        Assert.Equal(ConnectivityState.Tunneled, manager.CurrentState);
        // No poll loop should ever have run — an already-ready strategy costs nothing.
        Assert.True((after - before) < TimeSpan.FromSeconds(1));
    }

    // ── Discarded-fallback-result fix ────────────────────────────────────────

    private sealed class UnverifiedThenFailingStrategy : IConnectivityStrategy
    {
        private int _calls;

        public string Name => "UnverifiedThenFailing";
        public int Priority => 1;
        public ConnectivityType Type => ConnectivityType.PortForward;
        public int TeardownCount { get; private set; }

        public Task<ConnectivityResult> TryEstablishAsync(CancellationToken ct)
        {
            _calls++;
            return Task.FromResult(
                _calls == 1 ? ConnectivityResult.Assumed() : ConnectivityResult.Failed()
            );
        }

        public Task TeardownAsync()
        {
            TeardownCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EvaluateAsync_UnverifiedFallback_FailsOnRetry_DoesNotActivate_ReportsLocalOnly()
    {
        UnverifiedThenFailingStrategy strategy = new();
        ConnectivityManager manager = BuildManager(strategy);

        await manager.EvaluateAsync(CancellationToken.None);

        // The first attempt returned Assumed (held as a fallback); the retry at the end of the
        // loop returned Failed(). That Failed() must not be discarded and treated as a reason
        // to Activate() — the server has no working transport and must say so.
        Assert.Equal(ConnectivityState.LocalOnly, manager.CurrentState);
        Assert.Equal(ConnectivityType.LocalOnly, manager.ActiveStrategy);
    }
}
