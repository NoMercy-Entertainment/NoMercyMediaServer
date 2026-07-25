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
using NoMercy.Storage.Drivers.Local;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait("Category", "Unit")]
public sealed class ConnectivityManagerStateTests
{
    private static NetworkDiscovery BuildNetworkDiscovery()
    {
        NetworkDiscovery d = new(
            NullLogger<NetworkDiscovery>.Instance,
            new LocalStorageDriver(),
            new AuthTokenStore(),
            new ConnectivityStatus(),
            new()
        );
        d.ExternalIp = "1.2.3.4";
        return d;
    }

    private sealed class StubStrategy(
        string name,
        int priority,
        ConnectivityType type,
        bool succeeds
    ) : IConnectivityStrategy
    {
        public string Name => name;
        public int Priority => priority;
        public ConnectivityType Type => type;
        public bool WasAttempted { get; private set; }

        public Task<bool> TryEstablishAsync(CancellationToken ct)
        {
            WasAttempted = true;
            return Task.FromResult(succeeds);
        }

        public Task TeardownAsync() => Task.CompletedTask;
    }

    private static ConnectivityManager BuildManager(params IConnectivityStrategy[] strategies)
    {
        AuthTokenStore tokenStore = new();
        tokenStore.SetAccessToken("test-token");
        BootStatus boot = new();
        boot.MarkStarted();

        return new(
            NullLogger<ConnectivityManager>.Instance,
            tokenStore,
            BuildNetworkDiscovery(),
            strategies,
            boot
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenFirstStrategySucceeds_SetsDirectAccessState()
    {
        StubStrategy winning = new("PortForward", 1, ConnectivityType.PortForward, succeeds: true);
        StubStrategy skipped = new("Stun", 2, ConnectivityType.StunHolePunch, succeeds: true);
        ConnectivityManager manager = BuildManager(winning, skipped);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal(ConnectivityState.DirectAccess, manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllStrategiesFail_SetsLocalOnlyState()
    {
        StubStrategy a = new("PortForward", 1, ConnectivityType.PortForward, succeeds: false);
        StubStrategy b = new("Stun", 2, ConnectivityType.StunHolePunch, succeeds: false);
        ConnectivityManager manager = BuildManager(a, b);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal(ConnectivityState.LocalOnly, manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoStrategies_SetsLocalOnlyState()
    {
        ConnectivityManager manager = BuildManager();

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal(ConnectivityState.LocalOnly, manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_FirstStrategyFails_SecondSucceeds_SetsHolePunchedState()
    {
        StubStrategy fail = new("PortForward", 1, ConnectivityType.PortForward, succeeds: false);
        StubStrategy win = new("Stun", 2, ConnectivityType.StunHolePunch, succeeds: true);
        ConnectivityManager manager = BuildManager(fail, win);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal(ConnectivityState.HolePunched, manager.CurrentState);
        Assert.True(fail.WasAttempted);
        Assert.True(win.WasAttempted);
    }

    [Fact]
    public async Task EvaluateAsync_CloudflareTunnelSucceeds_SetsTunneledState()
    {
        StubStrategy cf = new(
            "CloudflareTunnel",
            3,
            ConnectivityType.CloudflareTunnel,
            succeeds: true
        );
        ConnectivityManager manager = BuildManager(cf);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal(ConnectivityState.Tunneled, manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_FirstSuccessWins_LaterStrategyNotAttempted()
    {
        StubStrategy first = new("PortForward", 1, ConnectivityType.PortForward, succeeds: true);
        StubStrategy second = new("Stun", 2, ConnectivityType.StunHolePunch, succeeds: true);
        ConnectivityManager manager = BuildManager(first, second);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.True(first.WasAttempted);
        Assert.False(second.WasAttempted);
    }

    [Fact]
    public async Task EvaluateAsync_StateChangedEvent_FiresWithCorrectState()
    {
        StubStrategy strategy = new("PortForward", 1, ConnectivityType.PortForward, succeeds: true);
        ConnectivityManager manager = BuildManager(strategy);
        List<ConnectivityState> observed = [];
        manager.StateChanged += s => observed.Add(s);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Contains(ConnectivityState.DirectAccess, observed);
    }

    [Fact]
    public async Task EvaluateAsync_AllFail_ActiveStrategyIsLocalOnly()
    {
        StubStrategy fail = new("PortForward", 1, ConnectivityType.PortForward, succeeds: false);
        ConnectivityManager manager = BuildManager(fail);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal(ConnectivityType.LocalOnly, manager.ActiveStrategy);
    }

    [Fact]
    public async Task EvaluateAsync_StrategiesOrderedByPriority_LowerPriorityTriedFirst()
    {
        List<string> attemptOrder = [];

        OrderTrackingStrategy low = new("Low", 1, ConnectivityType.PortForward, attemptOrder);
        OrderTrackingStrategy high = new("High", 2, ConnectivityType.StunHolePunch, attemptOrder);

        ConnectivityManager manager = BuildManager(high, low);

        await manager.EvaluateAsync(CancellationToken.None);

        Assert.Equal("Low", attemptOrder[0]);
        Assert.Equal("High", attemptOrder[1]);
    }

    private sealed class OrderTrackingStrategy(
        string name,
        int priority,
        ConnectivityType type,
        List<string> log
    ) : IConnectivityStrategy
    {
        public string Name => name;
        public int Priority => priority;
        public ConnectivityType Type => type;

        public Task<bool> TryEstablishAsync(CancellationToken ct)
        {
            log.Add(name);
            return Task.FromResult(false);
        }

        public Task TeardownAsync() => Task.CompletedTask;
    }
}
