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
    }

    private static PortForwardStrategy BuildStrategy(
        ConnectivityStatus status,
        bool portOpen = false
    )
    {
        return new PortForwardStrategy(
            new StubNetworkDiscovery(portOpen),
            status,
            NullLogger<PortForwardStrategy>.Instance
        );
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsOpen_ReturnsTrue_WithoutCheckingPort()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Open };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NatStatus.Open, status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsFiltered_SetsPortForwarded_AndPromotesToOpen()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.True(result);
        Assert.True(status.PortForwarded);
        Assert.Equal(NatStatus.Open, status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsNone_AndPortOpen_ReturnsTrueAndSetsOpen()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.None };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: true);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NatStatus.Open, status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsNone_AndPortClosed_ReturnsFalse()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.None };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsClosed_AndPortClosed_ReturnsFalse()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Closed };
        PortForwardStrategy strategy = BuildStrategy(status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsFiltered_DoesNotCallIsPortOpen()
    {
        TrackingNetworkDiscovery tracking = new();
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = new(
            tracking,
            status,
            NullLogger<PortForwardStrategy>.Instance
        );

        await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(tracking.IsPortOpenCalled);
    }

    [Fact]
    public void Priority_IsOne()
    {
        PortForwardStrategy strategy = BuildStrategy(new ConnectivityStatus());

        Assert.Equal(1, strategy.Priority);
    }

    [Fact]
    public void Type_IsPortForward()
    {
        PortForwardStrategy strategy = BuildStrategy(new ConnectivityStatus());

        Assert.Equal(ConnectivityType.PortForward, strategy.Type);
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
    }
}
