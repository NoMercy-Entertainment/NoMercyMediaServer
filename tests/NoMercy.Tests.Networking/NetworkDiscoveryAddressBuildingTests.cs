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
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.Storage.Drivers.Local;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: InternalDomain/ExternalDomain must derive a DNS-safe, dashed
/// hostname from the raw IP (dots/colons replaced), suffixed with this
/// device's id and the configured DNS suffix; InternalAddress/ExternalAddress
/// must be an https URL combining that domain with the configured port. This
/// mandate is the exact "dashed-host derivation + URL building" surface the
/// registration and connectivity flows depend on — a regression here breaks
/// every client's ability to resolve the server.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NetworkDiscoveryAddressBuildingTests
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

    public NetworkDiscoveryAddressBuildingTests()
    {
        // Every test in this file asserts against the non-synthesized suffix
        // unless it explicitly flips the flag — restore the default so test
        // order never leaks state across this static, process-wide setting.
        RuntimeServerSettings.Current.UseSynthesizedDns = false;
        RuntimeServerSettings.Current.InternalServerPort = 7626;
        RuntimeServerSettings.Current.ExternalServerPort = 7626;
    }

    [Fact]
    public void InternalDomain_DashesTheIp_AndAppendsDeviceIdAndSuffix()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "192.168.1.50";

        string domain = discovery.InternalDomain;

        Assert.Equal($"192-168-1-50.{Info.DeviceId}.nomercy.tv", domain);
    }

    [Fact]
    public void ExternalDomain_DashesTheIp_AndAppendsDeviceIdAndSuffix()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "203.0.113.42";

        string domain = discovery.ExternalDomain;

        Assert.Equal($"203-0-113-42.{Info.DeviceId}.nomercy.tv", domain);
    }

    [Fact]
    public void InternalDomain_DashedIpSegment_HasNoRawDotsOrColons()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "10.0.0.5";

        string domain = discovery.InternalDomain;
        // The first label is the dashed IP; only structural dots separating
        // it from DeviceId and the DNS suffix should remain.
        string dashedIpSegment = domain.Split('.')[0];

        Assert.Equal("10-0-0-5", dashedIpSegment);
        Assert.DoesNotContain(':', dashedIpSegment);
    }

    [Fact]
    public void InternalAddress_IsHttps_AndContainsInternalDomainAndPort()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "192.168.1.50";
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        string address = discovery.InternalAddress;

        Assert.Equal($"https://{discovery.InternalDomain}:7626", address);
    }

    [Fact]
    public void ExternalAddress_IsHttps_AndContainsExternalDomainAndPort()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "203.0.113.42";
        RuntimeServerSettings.Current.ExternalServerPort = 8443;

        string address = discovery.ExternalAddress;

        Assert.Equal($"https://{discovery.ExternalDomain}:8443", address);

        RuntimeServerSettings.Current.ExternalServerPort = 7626;
    }

    [Fact]
    public void InternalAddress_DifferentPortsForInternalAndExternal_AreIndependent()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "10.0.0.5";
        discovery.ExternalIp = "10.0.0.5";
        RuntimeServerSettings.Current.InternalServerPort = 7626;
        RuntimeServerSettings.Current.ExternalServerPort = 9000;

        string internalAddress = discovery.InternalAddress;
        string externalAddress = discovery.ExternalAddress;

        Assert.EndsWith(":7626", internalAddress);
        Assert.EndsWith(":9000", externalAddress);

        RuntimeServerSettings.Current.ExternalServerPort = 7626;
    }

    [Fact]
    public void InternalDomain_WhenUseSynthesizedDnsEnabled_UsesSrvSuffix()
    {
        RuntimeServerSettings.Current.UseSynthesizedDns = true;
        try
        {
            NetworkDiscovery discovery = BuildDiscovery();
            discovery.InternalIp = "192.168.1.50";

            string domain = discovery.InternalDomain;

            Assert.EndsWith("srv.nomercy.tv", domain);
        }
        finally
        {
            RuntimeServerSettings.Current.UseSynthesizedDns = false;
        }
    }

    [Fact]
    public void ExternalDomain_WhenUseSynthesizedDnsDisabled_UsesPlainSuffix()
    {
        RuntimeServerSettings.Current.UseSynthesizedDns = false;
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.ExternalIp = "203.0.113.42";

        string domain = discovery.ExternalDomain;

        Assert.EndsWith("nomercy.tv", domain);
        Assert.DoesNotContain("srv.nomercy.tv", domain);
    }

    [Fact]
    public void InternalIp_SetSameValueTwice_DoesNotThrow_AndKeepsValue()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "10.0.0.9";

        Exception? ex = Record.Exception(() => discovery.InternalIp = "10.0.0.9");

        Assert.Null(ex);
        Assert.Equal("10.0.0.9", discovery.InternalIp);
    }

    [Fact]
    public void InternalDomain_ContainsThisDeviceId()
    {
        NetworkDiscovery discovery = BuildDiscovery();
        discovery.InternalIp = "192.168.1.50";

        string domain = discovery.InternalDomain;

        Assert.Contains(Info.DeviceId.ToString(), domain);
    }
}
