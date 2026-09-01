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
using NoMercy.Networking.Connectivity.Strategies;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Status;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait("Category", "Unit")]
public sealed class PortForwardStrategyTests
{
    private sealed class StubNetworkDiscovery : INetworkDiscovery
    {
        private readonly bool _portOpen;

        public StubNetworkDiscovery(bool portOpen = false)
        {
            _portOpen = portOpen;
        }

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

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync() => Task.FromResult(_portOpen);

        public Task RemovePortMappingsAsync() => Task.CompletedTask;
    }

    private static PortForwardStrategy BuildStrategy(
        ConnectivityStatus status,
        bool portOpen = false
    )
    {
        return new(
            new StubNetworkDiscovery(portOpen),
            status,
            NullLogger<PortForwardStrategy>.Instance
        );
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTheProbeConnects_IsVerified()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.None };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: true);

        ConnectivityResult result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.True(result.Established);
        Assert.Equal(ConnectivityConfidence.Verified, result.Confidence);
        Assert.Equal(NatStatus.Open, status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenUpnpMappedButTheProbeFails_IsOnlyAssumed()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        ConnectivityResult result = await strategy.TryEstablishAsync(CancellationToken.None);

        // A router that accepts the UPnP call and drops the mapping is indistinguishable
        // from one that forwards correctly but will not hairpin. Reporting the first as
        // established fact is what pinned servers to a port forward that did not exist.
        Assert.True(result.Established);
        Assert.Equal(ConnectivityConfidence.Assumed, result.Confidence);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenUpnpMappedButUnproven_DoesNotClaimNatIsOpen()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        await strategy.TryEstablishAsync(CancellationToken.None);

        // NatStatus is reported to the API as stun_nat_type. Promoting an unconfirmed
        // mapping to Open told the control plane the server was directly reachable.
        Assert.Equal(NatStatus.Filtered, status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_AlwaysProbes_EvenWhenAnEarlierPassLeftNatOpen()
    {
        TrackingNetworkDiscovery tracking = new();
        ConnectivityStatus status = new() { NatStatus = NatStatus.Open };
        PortForwardStrategy strategy = new(
            tracking,
            status,
            NullLogger<PortForwardStrategy>.Instance
        );

        ConnectivityResult result = await strategy.TryEstablishAsync(CancellationToken.None);

        // A stale Open describes the network the server used to be on, and re-evaluation
        // happens precisely because that network changed.
        Assert.True(tracking.IsPortOpenCalled);
        Assert.False(result.Established);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsNone_AndPortClosed_Fails()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.None };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        ConnectivityResult result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result.Established);
        Assert.Equal(ConnectivityConfidence.None, result.Confidence);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsClosed_AndPortClosed_Fails()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Closed };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        ConnectivityResult result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result.Established);
    }

    [Fact]
    public async Task TeardownAsync_ClearsPortForwarded_SoAnotherTransportDoesNotInheritIt()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        await strategy.TryEstablishAsync(CancellationToken.None);
        Assert.True(status.PortForwarded);

        await strategy.TeardownAsync();

        Assert.False(status.PortForwarded);
    }

    [Fact]
    public void Priority_IsOne()
    {
        PortForwardStrategy strategy = BuildStrategy(new());

        Assert.Equal(1, strategy.Priority);
    }

    [Fact]
    public void Type_IsPortForward()
    {
        PortForwardStrategy strategy = BuildStrategy(new());

        Assert.Equal(ConnectivityType.PortForward, strategy.Type);
    }

    [Fact]
    public void Name_IsPortForward()
    {
        PortForwardStrategy strategy = BuildStrategy(new());

        Assert.Equal("PortForward", strategy.Name);
    }

    [Fact]
    public async Task TeardownAsync_DoesNotThrow_AndCompletes()
    {
        PortForwardStrategy strategy = BuildStrategy(new());

        Exception? ex = await Record.ExceptionAsync(strategy.TeardownAsync);

        Assert.Null(ex);
    }

    private sealed class TrackingNetworkDiscovery : INetworkDiscovery
    {
        public bool IsPortOpenCalled { get; private set; }
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

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync()
        {
            IsPortOpenCalled = true;
            return Task.FromResult(false);
        }

        public Task RemovePortMappingsAsync() => Task.CompletedTask;
    }
}
