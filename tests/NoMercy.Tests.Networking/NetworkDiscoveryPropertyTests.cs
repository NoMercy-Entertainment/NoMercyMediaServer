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
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;
using NoMercy.Storage.Drivers.Local;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait("Category", "Unit")]
public sealed class NetworkDiscoveryPropertyTests
{
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
    public void ExternalIp_DefaultFallback_IsZeroZeroZeroZero()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string ip = discovery.ExternalIp;

        Assert.Equal("0.0.0.0", ip);
    }

    [Fact]
    public void ExternalIp_AfterSet_ReturnsCachedValue()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "203.0.113.42";

        string ip = discovery.ExternalIp;

        Assert.Equal("203.0.113.42", ip);
    }

    [Fact]
    public void ExternalIp_SetSameValueTwice_DoesNotThrow()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "1.2.3.4";

        Exception? ex = Record.Exception(() => discovery.ExternalIp = "1.2.3.4");

        Assert.Null(ex);
        Assert.Equal("1.2.3.4", discovery.ExternalIp);
    }

    [Fact]
    public void ExternalIp_SetThenOverwrite_ReturnsLatestValue()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "1.2.3.4";
        discovery.ExternalIp = "5.6.7.8";

        Assert.Equal("5.6.7.8", discovery.ExternalIp);
    }

    [Fact]
    public void RegistrationInternalIp_WhenInternalIpIsLoopback_ReturnsZeroSentinel()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "127.0.0.1";

        string regIp = discovery.RegistrationInternalIp;

        Assert.Equal("0.0.0.0", regIp);
    }

    [Fact]
    public void RegistrationInternalIp_WhenInternalIpIsEmpty_ReturnsZeroSentinel()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = string.Empty;

        string regIp = discovery.RegistrationInternalIp;

        Assert.Equal("0.0.0.0", regIp);
    }

    [Fact]
    public void RegistrationInternalIp_WhenInternalIpIsRealLanIp_ReturnsRealIp()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "192.168.1.50";

        string regIp = discovery.RegistrationInternalIp;

        Assert.Equal("192.168.1.50", regIp);
    }

    [Fact]
    public void Ipv6Enabled_ReturnsFalse()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        bool enabled = discovery.Ipv6Enabled;

        Assert.False(enabled);
    }

    [Fact]
    public void ExternalAddressV6_WhenExternalIpV6IsNull_ReturnsNull()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIpV6 = null;

        string? addr = discovery.ExternalAddressV6;

        Assert.Null(addr);
    }

    [Fact]
    public void ExternalAddressV6_WhenExternalIpV6IsSet_ContainsBracketedAddress()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIpV6 = "2001:db8::1";

        string? addr = discovery.ExternalAddressV6;

        Assert.NotNull(addr);
        Assert.Contains("[2001:db8::1]", addr);
    }
}
