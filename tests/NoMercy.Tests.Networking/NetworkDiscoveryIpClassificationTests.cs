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
[Trait(name: "Category", value: "Unit")]
public sealed class NetworkDiscoveryIpClassificationTests
{
    [Theory]
    [InlineData(data: "172.17.0.1")]
    [InlineData(data: "172.18.5.5")]
    [InlineData(data: "172.20.0.1")]
    [InlineData(data: "172.31.255.255")]
    [InlineData(data: "172.16.0.1")] // WSL range
    public void IsDockerOrWslAddress_ContainerRanges_ReturnsTrue(string ip)
    {
        IPAddress address = IPAddress.Parse(ipString: ip);

        bool result = NetworkDiscovery.IsDockerOrWslAddress(address: address);

        Assert.True(condition: result);
    }

    [Theory]
    [InlineData(data: "192.168.1.1")]
    [InlineData(data: "10.0.0.5")]
    [InlineData(data: "172.15.0.1")] // just below the Docker/WSL band
    [InlineData(data: "172.32.0.1")] // just above the Docker band
    [InlineData(data: "127.0.0.1")]
    [InlineData(data: "8.8.8.8")]
    public void IsDockerOrWslAddress_NonContainerRanges_ReturnsFalse(string ip)
    {
        IPAddress address = IPAddress.Parse(ipString: ip);

        bool result = NetworkDiscovery.IsDockerOrWslAddress(address: address);

        Assert.False(condition: result);
    }

    [Fact]
    public void IsDockerOrWslAddress_Ipv6Address_ReturnsFalse()
    {
        IPAddress address = IPAddress.Parse(ipString: "2001:db8::1");

        bool result = NetworkDiscovery.IsDockerOrWslAddress(address: address);

        Assert.False(condition: result);
    }

    [Theory]
    [InlineData(data: ["Hyper-V Virtual Ethernet Adapter", "vEthernet"])]
    [InlineData(data: ["Docker Desktop Virtual Adapter", "docker0"])]
    [InlineData(data: ["Windows Subsystem for Linux", "wsl-eth"])]
    [InlineData(data: ["Cisco AnyConnect VPN adapter", "vpn0"])]
    [InlineData(data: ["VMware Virtual Ethernet Adapter", "vmnet1"])]
    [InlineData(data: ["VirtualBox Host-Only Ethernet Adapter", "vbox0"])]
    public void IsVirtualNetworkInterface_KnownVirtualAdapters_ReturnsTrue(
        string description,
        string name
    )
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(description: description, name: name);

        Assert.True(condition: result);
    }

    [Theory]
    [InlineData(data: ["Realtek PCIe GbE Family Controller", "Ethernet"])]
    [InlineData(data: ["Intel(R) Wi-Fi 6 AX201", "Wi-Fi"])]
    public void IsVirtualNetworkInterface_RealAdapters_ReturnsFalse(string description, string name)
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(description: description, name: name);

        Assert.False(condition: result);
    }

    [Fact]
    public void IsVirtualNetworkInterface_MatchIsCaseInsensitive()
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(description: "DOCKER Adapter", name: "ETH0");

        Assert.True(condition: result);
    }

    [Fact]
    public void IsVirtualNetworkInterface_KeywordOnlyInName_StillMatches()
    {
        bool result = NetworkDiscovery.IsVirtualNetworkInterface(
            description: "Generic network adapter",
            name: "docker0"
        );

        Assert.True(condition: result);
    }

    // -- Real (non-mocked) socket-based resolution: exercises GetInternalIp /
    // ResolveInternalIp / GetInternalIpV6 end to end on this machine's real
    // network stack. Deterministic in shape (a parseable address or the
    // documented fallback), even though the concrete value is machine-local.

    private static NetworkDiscovery BuildDiscovery()
    {
        return new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );
    }

    [Fact]
    public void InternalIp_WhenNeverExplicitlySet_ResolvesToParseableIpv4()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string ip = discovery.InternalIp;

        Assert.True(
            condition: IPAddress.TryParse(ipString: ip, address: out IPAddress? parsed),
            userMessage: $"'{ip}' must be a parseable IP address"
        );
        Assert.Equal(expected: System.Net.Sockets.AddressFamily.InterNetwork, actual: parsed!.AddressFamily);
    }

    [Fact]
    public void InternalIp_WhenNeverExplicitlySet_IsNeverAContainerAddress()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string ip = discovery.InternalIp;

        Assert.False(condition: NetworkDiscovery.IsDockerOrWslAddress(address: IPAddress.Parse(ipString: ip)));
    }

    [Fact]
    public void InternalIp_Getter_IsIdempotent_OnceResolved()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string first = discovery.InternalIp;
        string second = discovery.InternalIp;

        Assert.Equal(expected: first, actual: second);
    }

    [Fact]
    public void InternalIpV6_ReturnsNullOrAParseableIpv6Address()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string? ip = discovery.InternalIpV6;

        if (ip is null)
            return;

        Assert.True(condition: IPAddress.TryParse(ipString: ip, address: out IPAddress? parsed));
        Assert.Equal(expected: System.Net.Sockets.AddressFamily.InterNetworkV6, actual: parsed!.AddressFamily);
    }
}
