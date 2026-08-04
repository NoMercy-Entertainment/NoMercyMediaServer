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

using NoMercy.Networking.Cast;
using Xunit;

namespace NoMercy.Tests.Networking;

// A device row holds two addresses: LanIp, written by the mDNS scanner, is where the
// device sits on this network; Ip is where it last connected FROM, which for anyone
// watching through the tunnel is their public address. Every cast path read Ip, so a TV
// that had been watching from outside sent its LAUNCH at the owner's own router.
[Trait("Category", "Unit")]
public sealed class CastAddressTests
{
    [Fact]
    public void Resolve_PrefersTheLanAddressOverWhereTheDeviceConnectedFrom()
    {
        string? address = CastAddress.Resolve(lanIp: "192.168.2.31", connectedFromIp: "10.0.0.9");

        Assert.Equal("192.168.2.31", address);
    }

    [Fact]
    public void Resolve_TvSeenOnlyThroughTheTunnel_YieldsNoAddress()
    {
        string? address = CastAddress.Resolve(lanIp: null, connectedFromIp: "203.0.113.7");

        Assert.Null(address);
    }

    [Fact]
    public void Resolve_LanAddressWins_EvenWhenTheDeviceConnectedFromOutside()
    {
        string? address = CastAddress.Resolve(
            lanIp: "192.168.2.31",
            connectedFromIp: "203.0.113.7"
        );

        Assert.Equal("192.168.2.31", address);
    }

    [Fact]
    public void Resolve_NoLanSighting_FallsBackToAConnectionFromThisNetwork()
    {
        string? address = CastAddress.Resolve(lanIp: null, connectedFromIp: "192.168.2.31");

        Assert.Equal("192.168.2.31", address);
    }

    [Fact]
    public void Resolve_NothingRecorded_YieldsNoAddress()
    {
        Assert.Null(CastAddress.Resolve(null, null));
        Assert.Null(CastAddress.Resolve(string.Empty, string.Empty));
    }

    [Fact]
    public void Resolve_GarbageInEitherColumn_YieldsNoAddress()
    {
        Assert.Null(CastAddress.Resolve("not-an-ip", "also-not-an-ip"));
    }

    [Theory]
    [InlineData("192.168.2.31", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.4", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("203.0.113.7", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("", false)]
    public void IsOnThisNetwork_SeparatesReachableFromRoutable(string ip, bool expected)
    {
        Assert.Equal(expected, CastAddress.IsOnThisNetwork(ip));
    }
}
