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
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;
using NoMercy.Storage.Drivers.Local;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: internal-IP discovery must never advertise a container-only
/// address (Docker default bridge 172.17-31.0.0/16, WSL 172.16.x.x) as the
/// LAN IP, and must never classify a virtualization NIC (Hyper-V, Docker,
/// WSL, VPN, VMware, VirtualBox) as the routable interface. These are pure
/// classification rules — no NIC hardware needed to prove them.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NetworkDiscoveryIpClassificationTests
{
    [Theory]
    [InlineData("172.17.0.1")]
    [InlineData("172.18.5.5")]
    [InlineData("172.20.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("172.16.0.1")] // WSL range
    public void IsDockerOrWslAddress_ContainerRanges_ReturnsTrue(string ip)
    {
        IPAddress address = IPAddress.Parse(ip);

        bool result = NetworkDiscovery.IsDockerOrWslAddress(address);

        Assert.True(result);
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.15.0.1")] // just below the Docker/WSL band
    [InlineData("172.32.0.1")] // just above the Docker band
    [InlineData("127.0.0.1")]
    [InlineData("8.8.8.8")]
    public void IsDockerOrWslAddress_NonContainerRanges_ReturnsFalse(string ip)
    {
        IPAddress address = IPAddress.Parse(ip);

        bool result = NetworkDiscovery.IsDockerOrWslAddress(address);

        Assert.False(result);
    }

    [Fact]
    public void IsDockerOrWslAddress_Ipv6Address_ReturnsFalse()
    {
        IPAddress address = IPAddress.Parse("2001:db8::1");

        bool result = NetworkDiscovery.IsDockerOrWslAddress(address);

        Assert.False(result);
    }

    [Theory]
    [InlineData(["Hyper-V Virtual Ethernet Adapter", "vEthernet"])]
    [InlineData(["Docker Desktop Virtual Adapter", "docker0"])]
    [InlineData(["Windows Subsystem for Linux", "wsl-eth"])]
    [InlineData(["Cisco AnyConnect VPN adapter", "vpn0"])]
    [InlineData(["VMware Virtual Ethernet Adapter", "vmnet1"])]
    [InlineData(["VirtualBox Host-Only Ethernet Adapter", "vbox0"])]
    public void IsVirtualNetworkInterface_KnownVirtualAdapters_ReturnsTrue(
        string description,
        string name
    )
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(description, name);

        Assert.True(result);
    }

    [Theory]
    [InlineData(["Realtek PCIe GbE Family Controller", "Ethernet"])]
    [InlineData(["Intel(R) Wi-Fi 6 AX201", "Wi-Fi"])]
    public void IsVirtualNetworkInterface_RealAdapters_ReturnsFalse(string description, string name)
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(description, name);

        Assert.False(result);
    }

    [Fact]
    public void IsVirtualNetworkInterface_MatchIsCaseInsensitive()
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface("DOCKER Adapter", "ETH0");

        Assert.True(result);
    }

    [Fact]
    public void IsVirtualNetworkInterface_KeywordOnlyInName_StillMatches()
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(
            "Generic network adapter",
            "docker0"
        );

        Assert.True(result);
    }

    // -- Real (non-mocked) socket-based resolution: exercises GetInternalIp /
    // ResolveInternalIp / GetInternalIpV6 end to end on this machine's real
    // network stack. Deterministic in shape (a parseable address or the
    // documented fallback), even though the concrete value is machine-local.

    private static NetworkDiscovery BuildDiscovery()
    {
        return new(
            NullLogger<NetworkDiscovery>.Instance,
            new LocalStorageDriver(),
            new AuthTokenStore(),
            new ConnectivityStatus(),
            new()
        );
    }

    [Fact]
    public void InternalIp_WhenNeverExplicitlySet_ResolvesToParseableIpv4()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string ip = discovery.InternalIp;

        Assert.True(
            IPAddress.TryParse(ip, out IPAddress? parsed),
            $"'{ip}' must be a parseable IP address"
        );
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, parsed!.AddressFamily);
    }

    [Fact]
    public void InternalIp_WhenNeverExplicitlySet_IsNeverAContainerAddress()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string ip = discovery.InternalIp;

        Assert.False(NetworkDiscovery.IsDockerOrWslAddress(IPAddress.Parse(ip)));
    }

    [Fact]
    public void InternalIp_Getter_IsIdempotent_OnceResolved()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string first = discovery.InternalIp;
        string second = discovery.InternalIp;

        Assert.Equal(first, second);
    }

    [Fact]
    public void InternalIpV6_ReturnsNullOrAParseableIpv6Address()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string? ip = discovery.InternalIpV6;

        if (ip is null)
            return;

        Assert.True(IPAddress.TryParse(ip, out IPAddress? parsed));
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetworkV6, parsed!.AddressFamily);
    }
}
