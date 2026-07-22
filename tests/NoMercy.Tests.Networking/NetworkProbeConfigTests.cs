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
using NoMercy.Networking.Discovery;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait(name: "Category", value: "Unit")]
public sealed class NetworkProbeConfigTests
{
    [Fact]
    public void DefaultProbeTargets_AreNotEmpty()
    {
        NetworkProbeConfig config = new();

        Assert.NotEmpty(collection: config.ProbeTargets);
    }

    [Fact]
    public void DefaultProbeTargets_ContainsCloudflare()
    {
        NetworkProbeConfig config = new();

        Assert.Contains(expected: "1.1.1.1", collection: config.ProbeTargets);
    }

    [Fact]
    public void DefaultProbeTargets_ContainsGoogle()
    {
        NetworkProbeConfig config = new();

        Assert.Contains(expected: "8.8.8.8", collection: config.ProbeTargets);
    }

    [Fact]
    public void DefaultLocalIpDiscoveryIpv4_IsValidIpAddress()
    {
        NetworkProbeConfig config = new();

        bool valid = IPAddress.TryParse(ipString: config.LocalIpDiscoveryIpv4, address: out _);

        Assert.True(condition: valid);
    }

    [Fact]
    public void DefaultLocalIpDiscoveryIpv6_IsValidIpv6Address()
    {
        NetworkProbeConfig config = new();

        bool valid = IPAddress.TryParse(ipString: config.LocalIpDiscoveryIpv6, address: out IPAddress? addr);

        Assert.True(condition: valid);
        Assert.Equal(expected: System.Net.Sockets.AddressFamily.InterNetworkV6, actual: addr!.AddressFamily);
    }

    [Fact]
    public void DefaultLocalIpDiscoveryPort_IsEphemeralRange()
    {
        NetworkProbeConfig config = new();

        Assert.InRange(actual: config.LocalIpDiscoveryPort, low: 49152, high: 65535);
    }

    [Theory]
    [InlineData(data: "api.nomercy.tv")]
    [InlineData(data: "1.1.1.1")]
    [InlineData(data: "8.8.8.8")]
    public void DefaultProbeTargets_ContainsExpectedTarget(string target)
    {
        NetworkProbeConfig config = new();

        Assert.Contains(expected: target, collection: config.ProbeTargets);
    }

    [Fact]
    public void ProbeTargets_CanBeOverridden()
    {
        NetworkProbeConfig config = new() { ProbeTargets = ["custom.example.com"] };

        Assert.Single(collection: config.ProbeTargets);
        Assert.Equal(expected: "custom.example.com", actual: config.ProbeTargets[0]);
    }
}
