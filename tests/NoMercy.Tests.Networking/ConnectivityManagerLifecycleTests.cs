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
/// REQUIREMENT: ConnectivityManager's IHostedService lifecycle must (1) wait
/// for boot to finish and for authentication before evaluating strategies,
/// (2) give up cleanly (no throw, no hang) when cancelled while waiting, (3)
/// run the full evaluate-and-connect flow once boot+auth are ready, and (4)
/// tear down the active strategy on stop/dispose so a stopped server never
/// leaves a tunnel or port mapping dangling.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class ConnectivityManagerLifecycleTests
{
    private sealed class FastNetworkDiscovery : INetworkDiscovery
    {
        public int DiscoverCallCount { get; private set; }
        public string InternalIp { get; set; } = "192.168.1.1";
        public string RegistrationInternalIp => InternalIp;
        public string ExternalIp { get; set; } = "1.2.3.4";
        public string? InternalIpV6 => null;
        public string? ExternalIpV6 { get; set; }
        public string InternalDomain => string.Empty;
        public string InternalAddress => string.Empty;
        public string ExternalDomain => string.Empty;
        public string ExternalAddress => string.Empty;
        public string? ExternalAddressV6 => null;
        public bool Ipv6Enabled => false;

        public Task DiscoverExternalIpAsync()
        {
            DiscoverCallCount++;
            return Task.CompletedTask;
        }

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: false);
    }

    private sealed class RecordingStrategy(bool succeeds) : IConnectivityStrategy
    {
        public bool TeardownCalled { get; private set; }
        public string Name => "Recording";
        public int Priority => 1;
        public ConnectivityType Type => ConnectivityType.PortForward;

        public Task<bool> TryEstablishAsync(CancellationToken ct) => Task.FromResult(result: succeeds);

        public Task TeardownAsync()
        {
            TeardownCalled = true;
            return Task.CompletedTask;
        }
    }

    private static ConnectivityManager BuildManager(
        FastNetworkDiscovery discovery,
        BootStatus boot,
        AuthTokenStore tokenStore,
        params IConnectivityStrategy[] strategies
    )
    {
        return new(
            logger: NullLogger<ConnectivityManager>.Instance,
            authTokenStore: tokenStore,
            networkDiscovery: discovery,
            strategies: strategies,
            bootStatus: boot
        );
    }

    [Fact]
    public async Task StartAsync_BootAndAuthAlreadyReady_RunsEvaluateAsync()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        boot.MarkStarted();
        AuthTokenStore tokenStore = new();
        tokenStore.SetAccessToken(token: "test-token");
        RecordingStrategy strategy = new(succeeds: true);
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore, strategies: strategy);

        await manager.StartAsync(cancellationToken: CancellationToken.None);

        // ExecuteAsync runs on the background task; give it a moment to reach
        // EvaluateAsync (no real I/O — this is a fast in-process transition).
        for (int i = 0; i < 50 && manager.CurrentState != ConnectivityState.DirectAccess; i++)
            await Task.Delay(millisecondsDelay: 20);

        Assert.Equal(expected: ConnectivityState.DirectAccess, actual: manager.CurrentState);
        Assert.Equal(expected: 1, actual: discovery.DiscoverCallCount);

        await manager.StopAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_NoAuthToken_StopAsync_CancelsWaitLoop_WithoutThrowing()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        boot.MarkStarted();
        AuthTokenStore tokenStore = new(); // AccessToken stays null
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore);

        await manager.StartAsync(cancellationToken: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50); // let ExecuteAsync enter the "waiting for auth" loop

        Exception? ex = await Record.ExceptionAsync(testCode: () =>
            manager.StopAsync(cancellationToken: CancellationToken.None)
        );

        Assert.Null(@object: ex);
        // Never reached DiscoverExternalIpAsync — auth never arrived.
        Assert.Equal(expected: 0, actual: discovery.DiscoverCallCount);
    }

    [Fact]
    public async Task StartAsync_BootNeverStarted_StopAsync_CancelsWaitLoop_WithoutThrowing()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new(); // never marked started
        AuthTokenStore tokenStore = new();
        tokenStore.SetAccessToken(token: "test-token");
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore);

        await manager.StartAsync(cancellationToken: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);

        Exception? ex = await Record.ExceptionAsync(testCode: () =>
            manager.StopAsync(cancellationToken: CancellationToken.None)
        );

        Assert.Null(@object: ex);
        Assert.Equal(expected: 0, actual: discovery.DiscoverCallCount);
    }

    [Fact]
    public async Task StopAsync_WhenNeverStarted_ReturnsImmediately_WithoutThrowing()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        AuthTokenStore tokenStore = new();
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore);

        Exception? ex = await Record.ExceptionAsync(testCode: () =>
            manager.StopAsync(cancellationToken: CancellationToken.None)
        );

        Assert.Null(@object: ex);
    }

    [Fact]
    public async Task StopAsync_AfterSuccessfulEvaluate_TearsDownActiveStrategy()
    {
        // TeardownAsync on the active strategy only runs when StopAsync has an
        // _executingTask to wait on — i.e. only after StartAsync (not when
        // EvaluateAsync is invoked directly). Drive the real lifecycle so this
        // proves the actual StartAsync → EvaluateAsync → StopAsync path tears
        // down, matching what the hosted-service shutdown sequence does.
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        boot.MarkStarted();
        AuthTokenStore tokenStore = new();
        tokenStore.SetAccessToken(token: "test-token");
        RecordingStrategy strategy = new(succeeds: true);
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore, strategies: strategy);

        await manager.StartAsync(cancellationToken: CancellationToken.None);
        for (int i = 0; i < 50 && manager.CurrentState != ConnectivityState.DirectAccess; i++)
            await Task.Delay(millisecondsDelay: 20);

        await manager.StopAsync(cancellationToken: CancellationToken.None);

        Assert.True(condition: strategy.TeardownCalled);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        AuthTokenStore tokenStore = new();
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore);

        Exception? ex = Record.Exception(testCode: () =>
        {
            manager.Dispose();
            manager.Dispose();
        });

        Assert.Null(@object: ex);
    }

    [Fact]
    public void ActiveStrategy_BeforeAnyEvaluate_IsLocalOnly()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        AuthTokenStore tokenStore = new();
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore);

        Assert.Equal(expected: ConnectivityType.LocalOnly, actual: manager.ActiveStrategy);
    }

    [Fact]
    public void CurrentState_BeforeAnyEvaluate_IsStarting()
    {
        FastNetworkDiscovery discovery = new();
        BootStatus boot = new();
        AuthTokenStore tokenStore = new();
        ConnectivityManager manager = BuildManager(discovery: discovery, boot: boot, tokenStore: tokenStore);

        Assert.Equal(expected: ConnectivityState.Starting, actual: manager.CurrentState);
    }
}
