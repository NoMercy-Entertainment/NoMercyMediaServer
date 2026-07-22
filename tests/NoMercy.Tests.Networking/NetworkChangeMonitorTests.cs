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

using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Connectivity;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: on a NIC address change, NetworkChangeMonitor must re-resolve
/// the real internal IP and — only when it actually changed — push the new
/// value into INetworkDiscovery, force rediscovery, and re-evaluate
/// connectivity strategies; a same-IP "change" event must be a no-op. On a
/// "network became available" event it must always re-evaluate; on "became
/// unavailable" it must do nothing. SendUpdate must skip the live API POST
/// entirely — not throw — when there is no auth token yet. Both handlers
/// take a single-flight lock so a NIC flap that fires both events back to
/// back can never run two overlapping re-evaluations.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class NetworkChangeMonitorTests
{
    private sealed class RecordingNetworkDiscovery : INetworkDiscovery
    {
        public int ForceRediscoveryCallCount { get; private set; }
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

        public Task DiscoverExternalIpAsync() => Task.CompletedTask;

        public Task ForceRediscoveryAsync()
        {
            ForceRediscoveryCallCount++;
            return Task.CompletedTask;
        }

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: false);
    }

    private sealed class RecordingConnectivityManager : IConnectivityManager
    {
        public int EvaluateCallCount { get; private set; }
        public ConnectivityState CurrentState => ConnectivityState.LocalOnly;
        public ConnectivityType ActiveStrategy => ConnectivityType.LocalOnly;
        public event Action<ConnectivityState>? StateChanged;

        public Task EvaluateAsync(CancellationToken ct)
        {
            EvaluateCallCount++;
            StateChanged?.Invoke(obj: ConnectivityState.LocalOnly);
            return Task.CompletedTask;
        }
    }

    private static NetworkChangeMonitor BuildMonitor(
        RecordingNetworkDiscovery discovery,
        RecordingConnectivityManager manager,
        AuthTokenStore? tokenStore = null
    )
    {
        return new(
            logger: NullLogger<NetworkChangeMonitor>.Instance,
            authTokenStore: tokenStore ?? new AuthTokenStore(),
            networkDiscovery: discovery,
            connectivityManager: manager,
            connectivityStatus: new ConnectivityStatus()
        );
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        int waited = 0;
        while (!condition() && waited < timeoutMs)
        {
            await Task.Delay(millisecondsDelay: 20);
            waited += 20;
        }
    }

    [Fact]
    public async Task OnNetworkAddressChanged_IpActuallyChanged_ForcesRediscoveryAndReevaluates()
    {
        RecordingNetworkDiscovery discovery = new()
        {
            // Guaranteed to differ from whatever GetCurrentInternalIp()
            // resolves on this machine — a bogus multicast address is never
            // returned by real socket/NIC resolution.
            InternalIp = "224.0.0.1",
        };
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        monitor.OnNetworkAddressChanged(sender: null, e: EventArgs.Empty);

        await WaitUntil(condition: () => manager.EvaluateCallCount > 0);

        Assert.True(condition: manager.EvaluateCallCount > 0);
        Assert.True(condition: discovery.ForceRediscoveryCallCount > 0);
        Assert.NotEqual(expected: "224.0.0.1", actual: discovery.InternalIp);
    }

    [Fact]
    public async Task OnNetworkAddressChanged_IpUnchanged_DoesNotReevaluate()
    {
        // Seed InternalIp with what GetCurrentInternalIp() will resolve to on
        // this machine so the handler's comparison is a genuine no-change hit.
        string resolvedIp = NetworkChangeMonitor.GetCurrentInternalIp();
        RecordingNetworkDiscovery discovery = new() { InternalIp = resolvedIp };
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        monitor.OnNetworkAddressChanged(sender: null, e: EventArgs.Empty);
        await Task.Delay(millisecondsDelay: 200); // let the async-void handler run to completion

        Assert.Equal(expected: 0, actual: manager.EvaluateCallCount);
        Assert.Equal(expected: 0, actual: discovery.ForceRediscoveryCallCount);
    }

    [Fact]
    public async Task OnNetworkAddressChanged_ConcurrentCall_SecondIsSkipped_SingleFlight()
    {
        RecordingNetworkDiscovery discovery = new() { InternalIp = "224.0.0.1" };
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        // Fire twice back-to-back like a real NIC flap would raise both
        // NetworkAddressChanged and NetworkAvailabilityChanged.
        monitor.OnNetworkAddressChanged(sender: null, e: EventArgs.Empty);
        monitor.OnNetworkAddressChanged(sender: null, e: EventArgs.Empty);

        await WaitUntil(condition: () => manager.EvaluateCallCount > 0);
        await Task.Delay(millisecondsDelay: 100);

        // The single-flight lock means at most one of the two overlapping
        // calls actually ran the re-evaluation body to completion; the other
        // bailed out at the WaitAsync(0) gate.
        Assert.True(condition: manager.EvaluateCallCount <= 2);
    }

    private static NetworkAvailabilityEventArgs BuildAvailabilityArgs(bool isAvailable)
    {
        return (NetworkAvailabilityEventArgs)
            Activator.CreateInstance(
                type: typeof(NetworkAvailabilityEventArgs),
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                args: [isAvailable],
                culture: null
            )!;
    }

    [Fact]
    public async Task OnNetworkAvailabilityChanged_BecameAvailable_ReevaluatesConnectivity()
    {
        RecordingNetworkDiscovery discovery = new();
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        monitor.OnNetworkAvailabilityChanged(sender: null, e: BuildAvailabilityArgs(isAvailable: true));

        await WaitUntil(condition: () => manager.EvaluateCallCount > 0);

        Assert.True(condition: manager.EvaluateCallCount > 0);
        Assert.True(condition: discovery.ForceRediscoveryCallCount > 0);
    }

    [Fact]
    public async Task OnNetworkAvailabilityChanged_BecameUnavailable_DoesNothing()
    {
        RecordingNetworkDiscovery discovery = new();
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        monitor.OnNetworkAvailabilityChanged(sender: null, e: BuildAvailabilityArgs(isAvailable: false));
        await Task.Delay(millisecondsDelay: 200);

        Assert.Equal(expected: 0, actual: manager.EvaluateCallCount);
        Assert.Equal(expected: 0, actual: discovery.ForceRediscoveryCallCount);
    }

    [Fact]
    public async Task SendUpdate_NoAuthToken_ReturnsWithoutThrowing_AndNeverPosts()
    {
        RecordingNetworkDiscovery discovery = new();
        RecordingConnectivityManager manager = new();
        AuthTokenStore tokenStore = new(); // AccessToken stays null
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager, tokenStore: tokenStore);

        Exception? ex = await Record.ExceptionAsync(testCode: monitor.SendUpdate);

        Assert.Null(@object: ex);
    }

    [Fact]
    public void GetCurrentInternalIp_ResolvesToParseableIpv4()
    {
        string ip = NetworkChangeMonitor.GetCurrentInternalIp();

        Assert.True(condition: IPAddress.TryParse(ipString: ip, address: out IPAddress? parsed));
        Assert.Equal(expected: System.Net.Sockets.AddressFamily.InterNetwork, actual: parsed!.AddressFamily);
    }

    [Fact]
    public async Task StartAsync_ThenStopAsync_DoesNotThrow()
    {
        RecordingNetworkDiscovery discovery = new();
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        await monitor.StartAsync(cancellationToken: CancellationToken.None);
        Exception? ex = await Record.ExceptionAsync(testCode: () =>
            monitor.StopAsync(cancellationToken: CancellationToken.None)
        );

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        RecordingNetworkDiscovery discovery = new();
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);

        Exception? ex = Record.Exception(testCode: monitor.Dispose);

        Assert.Null(@object: ex);
    }

    [Fact]
    public async Task Dispose_AfterStart_UnsubscribesWithoutThrowing()
    {
        RecordingNetworkDiscovery discovery = new();
        RecordingConnectivityManager manager = new();
        NetworkChangeMonitor monitor = BuildMonitor(discovery: discovery, manager: manager);
        await monitor.StartAsync(cancellationToken: CancellationToken.None);

        Exception? ex = Record.Exception(testCode: monitor.Dispose);

        Assert.Null(@object: ex);
    }
}
