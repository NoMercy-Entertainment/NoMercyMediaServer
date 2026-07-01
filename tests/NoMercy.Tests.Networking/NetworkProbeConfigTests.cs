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

[Trait("Category", "Unit")]
public sealed class NetworkProbeConfigTests
{
    [Fact]
    public void DefaultProbeTargets_AreNotEmpty()
    {
        NetworkProbeConfig config = new();

        Assert.NotEmpty(config.ProbeTargets);
    }

    [Fact]
    public void DefaultProbeTargets_ContainsCloudflare()
    {
        NetworkProbeConfig config = new();

        Assert.Contains("1.1.1.1", config.ProbeTargets);
    }

    [Fact]
    public void DefaultProbeTargets_ContainsGoogle()
    {
        NetworkProbeConfig config = new();

        Assert.Contains("8.8.8.8", config.ProbeTargets);
    }

    [Fact]
    public void DefaultLocalIpDiscoveryIpv4_IsValidIpAddress()
    {
        NetworkProbeConfig config = new();

        bool valid = IPAddress.TryParse(config.LocalIpDiscoveryIpv4, out _);

        Assert.True(valid);
    }

    [Fact]
    public void DefaultLocalIpDiscoveryIpv6_IsValidIpv6Address()
    {
        NetworkProbeConfig config = new();

        bool valid = IPAddress.TryParse(config.LocalIpDiscoveryIpv6, out IPAddress? addr);

        Assert.True(valid);
        Assert.Equal(System.Net.Sockets.AddressFamily.InterNetworkV6, addr!.AddressFamily);
    }

    [Fact]
    public void DefaultLocalIpDiscoveryPort_IsEphemeralRange()
    {
        NetworkProbeConfig config = new();

        Assert.InRange(config.LocalIpDiscoveryPort, 49152, 65535);
    }

    [Theory]
    [InlineData("api.nomercy.tv")]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    public void DefaultProbeTargets_ContainsExpectedTarget(string target)
    {
        NetworkProbeConfig config = new();

        Assert.Contains(target, config.ProbeTargets);
    }

    [Fact]
    public void ProbeTargets_CanBeOverridden()
    {
        NetworkProbeConfig config = new() { ProbeTargets = ["custom.example.com"] };

        Assert.Single(config.ProbeTargets);
        Assert.Equal("custom.example.com", config.ProbeTargets[0]);
    }
}
