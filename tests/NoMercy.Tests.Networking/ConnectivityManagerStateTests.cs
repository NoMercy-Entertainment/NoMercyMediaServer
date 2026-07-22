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

[Trait(name: "Category", value: "Unit")]
public sealed class ConnectivityManagerStateTests
{
    private static NetworkDiscovery BuildNetworkDiscovery()
    {
        NetworkDiscovery d = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
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
            return Task.FromResult(result: succeeds);
        }

        public Task TeardownAsync() => Task.CompletedTask;
    }

    private static ConnectivityManager BuildManager(params IConnectivityStrategy[] strategies)
    {
        AuthTokenStore tokenStore = new();
        tokenStore.SetAccessToken(token: "test-token");
        BootStatus boot = new();
        boot.MarkStarted();

        return new(
            logger: NullLogger<ConnectivityManager>.Instance,
            authTokenStore: tokenStore,
            networkDiscovery: BuildNetworkDiscovery(),
            strategies: strategies,
            bootStatus: boot
        );
    }

    [Fact]
    public async Task EvaluateAsync_WhenFirstStrategySucceeds_SetsDirectAccessState()
    {
        StubStrategy winning = new(name: "PortForward", priority: 1, type: ConnectivityType.PortForward, succeeds: true);
        StubStrategy skipped = new(name: "Stun", priority: 2, type: ConnectivityType.StunHolePunch, succeeds: true);
        ConnectivityManager manager = BuildManager(strategies: [winning, skipped]);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: ConnectivityState.DirectAccess, actual: manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllStrategiesFail_SetsLocalOnlyState()
    {
        StubStrategy a = new(name: "PortForward", priority: 1, type: ConnectivityType.PortForward, succeeds: false);
        StubStrategy b = new(name: "Stun", priority: 2, type: ConnectivityType.StunHolePunch, succeeds: false);
        ConnectivityManager manager = BuildManager(strategies: [a, b]);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: ConnectivityState.LocalOnly, actual: manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoStrategies_SetsLocalOnlyState()
    {
        ConnectivityManager manager = BuildManager();

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: ConnectivityState.LocalOnly, actual: manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_FirstStrategyFails_SecondSucceeds_SetsHolePunchedState()
    {
        StubStrategy fail = new(name: "PortForward", priority: 1, type: ConnectivityType.PortForward, succeeds: false);
        StubStrategy win = new(name: "Stun", priority: 2, type: ConnectivityType.StunHolePunch, succeeds: true);
        ConnectivityManager manager = BuildManager(strategies: [fail, win]);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: ConnectivityState.HolePunched, actual: manager.CurrentState);
        Assert.True(condition: fail.WasAttempted);
        Assert.True(condition: win.WasAttempted);
    }

    [Fact]
    public async Task EvaluateAsync_CloudflareTunnelSucceeds_SetsTunneledState()
    {
        StubStrategy cf = new(
            name: "CloudflareTunnel",
            priority: 3,
            type: ConnectivityType.CloudflareTunnel,
            succeeds: true
        );
        ConnectivityManager manager = BuildManager(strategies: cf);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: ConnectivityState.Tunneled, actual: manager.CurrentState);
    }

    [Fact]
    public async Task EvaluateAsync_FirstSuccessWins_LaterStrategyNotAttempted()
    {
        StubStrategy first = new(name: "PortForward", priority: 1, type: ConnectivityType.PortForward, succeeds: true);
        StubStrategy second = new(name: "Stun", priority: 2, type: ConnectivityType.StunHolePunch, succeeds: true);
        ConnectivityManager manager = BuildManager(strategies: [first, second]);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.True(condition: first.WasAttempted);
        Assert.False(condition: second.WasAttempted);
    }

    [Fact]
    public async Task EvaluateAsync_StateChangedEvent_FiresWithCorrectState()
    {
        StubStrategy strategy = new(name: "PortForward", priority: 1, type: ConnectivityType.PortForward, succeeds: true);
        ConnectivityManager manager = BuildManager(strategies: strategy);
        List<ConnectivityState> observed = [];
        manager.StateChanged += s => observed.Add(item: s);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Contains(expected: ConnectivityState.DirectAccess, collection: observed);
    }

    [Fact]
    public async Task EvaluateAsync_AllFail_ActiveStrategyIsLocalOnly()
    {
        StubStrategy fail = new(name: "PortForward", priority: 1, type: ConnectivityType.PortForward, succeeds: false);
        ConnectivityManager manager = BuildManager(strategies: fail);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: ConnectivityType.LocalOnly, actual: manager.ActiveStrategy);
    }

    [Fact]
    public async Task EvaluateAsync_StrategiesOrderedByPriority_LowerPriorityTriedFirst()
    {
        List<string> attemptOrder = [];

        OrderTrackingStrategy low = new(name: "Low", priority: 1, type: ConnectivityType.PortForward, log: attemptOrder);
        OrderTrackingStrategy high = new(name: "High", priority: 2, type: ConnectivityType.StunHolePunch, log: attemptOrder);

        ConnectivityManager manager = BuildManager(strategies: [high, low]);

        await manager.EvaluateAsync(ct: CancellationToken.None);

        Assert.Equal(expected: "Low", actual: attemptOrder[index: 0]);
        Assert.Equal(expected: "High", actual: attemptOrder[index: 1]);
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
            log.Add(item: name);
            return Task.FromResult(result: false);
        }

        public Task TeardownAsync() => Task.CompletedTask;
    }
}
