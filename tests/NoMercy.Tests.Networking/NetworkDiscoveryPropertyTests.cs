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

[Trait(name: "Category", value: "Unit")]
public sealed class NetworkDiscoveryPropertyTests
{
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
    public void ExternalIp_DefaultFallback_IsZeroZeroZeroZero()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        string ip = discovery.ExternalIp;

        Assert.Equal(expected: "0.0.0.0", actual: ip);
    }

    [Fact]
    public void ExternalIp_AfterSet_ReturnsCachedValue()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "203.0.113.42";

        string ip = discovery.ExternalIp;

        Assert.Equal(expected: "203.0.113.42", actual: ip);
    }

    [Fact]
    public void ExternalIp_SetSameValueTwice_DoesNotThrow()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "1.2.3.4";

        Exception? ex = Record.Exception(testCode: () => discovery.ExternalIp = "1.2.3.4");

        Assert.Null(@object: ex);
        Assert.Equal(expected: "1.2.3.4", actual: discovery.ExternalIp);
    }

    [Fact]
    public void ExternalIp_SetThenOverwrite_ReturnsLatestValue()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "1.2.3.4";
        discovery.ExternalIp = "5.6.7.8";

        Assert.Equal(expected: "5.6.7.8", actual: discovery.ExternalIp);
    }

    [Fact]
    public void RegistrationInternalIp_WhenInternalIpIsLoopback_ReturnsZeroSentinel()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "127.0.0.1";

        string regIp = discovery.RegistrationInternalIp;

        Assert.Equal(expected: "0.0.0.0", actual: regIp);
    }

    [Fact]
    public void RegistrationInternalIp_WhenInternalIpIsEmpty_ReturnsZeroSentinel()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = string.Empty;

        string regIp = discovery.RegistrationInternalIp;

        Assert.Equal(expected: "0.0.0.0", actual: regIp);
    }

    [Fact]
    public void RegistrationInternalIp_WhenInternalIpIsRealLanIp_ReturnsRealIp()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "192.168.1.50";

        string regIp = discovery.RegistrationInternalIp;

        Assert.Equal(expected: "192.168.1.50", actual: regIp);
    }

    [Fact]
    public void Ipv6Enabled_ReturnsFalse()
    {
        NetworkDiscovery discovery = BuildDiscovery();

        bool enabled = discovery.Ipv6Enabled;

        Assert.False(condition: enabled);
    }

    [Fact]
    public void ExternalAddressV6_WhenExternalIpV6IsNull_ReturnsNull()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIpV6 = null;

        string? addr = discovery.ExternalAddressV6;

        Assert.Null(@object: addr);
    }

    [Fact]
    public void ExternalAddressV6_WhenExternalIpV6IsSet_ContainsBracketedAddress()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIpV6 = "2001:db8::1";

        string? addr = discovery.ExternalAddressV6;

        Assert.NotNull(@object: addr);
        Assert.Contains(expectedSubstring: "[2001:db8::1]", actualString: addr);
    }
}
