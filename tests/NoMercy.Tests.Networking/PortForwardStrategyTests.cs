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

[Trait(name: "Category", value: "Unit")]
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

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: _portOpen);
    }

    private static PortForwardStrategy BuildStrategy(
        ConnectivityStatus status,
        bool portOpen = false
    )
    {
        return new(
            networkDiscovery: new StubNetworkDiscovery(portOpen: portOpen),
            connectivityStatus: status,
            logger: NullLogger<PortForwardStrategy>.Instance
        );
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsOpen_ReturnsTrue_WithoutCheckingPort()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Open };
        PortForwardStrategy strategy = BuildStrategy(status: status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.True(condition: result);
        Assert.Equal(expected: NatStatus.Open, actual: status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsFiltered_SetsPortForwarded_AndPromotesToOpen()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = BuildStrategy(status: status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.True(condition: result);
        Assert.True(condition: status.PortForwarded);
        Assert.Equal(expected: NatStatus.Open, actual: status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsNone_AndPortOpen_ReturnsTrueAndSetsOpen()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.None };
        PortForwardStrategy strategy = BuildStrategy(status: status, portOpen: true);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.True(condition: result);
        Assert.Equal(expected: NatStatus.Open, actual: status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsNone_AndPortClosed_ReturnsFalse()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.None };
        PortForwardStrategy strategy = BuildStrategy(status: status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsClosed_AndPortClosed_ReturnsFalse()
    {
        ConnectivityStatus status = new() { NatStatus = NatStatus.Closed };
        PortForwardStrategy strategy = BuildStrategy(status: status, portOpen: false);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenNatStatusIsFiltered_DoesNotCallIsPortOpen()
    {
        TrackingNetworkDiscovery tracking = new();
        ConnectivityStatus status = new() { NatStatus = NatStatus.Filtered };
        PortForwardStrategy strategy = new(
            networkDiscovery: tracking,
            connectivityStatus: status,
            logger: NullLogger<PortForwardStrategy>.Instance
        );

        await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.False(condition: tracking.IsPortOpenCalled);
    }

    [Fact]
    public void Priority_IsOne()
    {
        PortForwardStrategy strategy = BuildStrategy(status: new());

        Assert.Equal(expected: 1, actual: strategy.Priority);
    }

    [Fact]
    public void Type_IsPortForward()
    {
        PortForwardStrategy strategy = BuildStrategy(status: new());

        Assert.Equal(expected: ConnectivityType.PortForward, actual: strategy.Type);
    }

    [Fact]
    public void Name_IsPortForward()
    {
        PortForwardStrategy strategy = BuildStrategy(status: new());

        Assert.Equal(expected: "PortForward", actual: strategy.Name);
    }

    [Fact]
    public async Task TeardownAsync_DoesNotThrow_AndCompletes()
    {
        PortForwardStrategy strategy = BuildStrategy(status: new());

        Exception? ex = await Record.ExceptionAsync(testCode: strategy.TeardownAsync);

        Assert.Null(@object: ex);
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
            return Task.FromResult(result: false);
        }
    }
}
