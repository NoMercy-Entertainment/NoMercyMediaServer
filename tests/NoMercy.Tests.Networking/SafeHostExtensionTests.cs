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

using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait("Category", "Unit")]
public sealed class SafeHostExtensionTests
{
    [Theory]
    [InlineData(["192.168.1.100", "192-168-1-100"])]
    [InlineData(["10.0.0.1", "10-0-0-1"])]
    [InlineData(["172.16.5.50", "172-16-5-50"])]
    [InlineData(["0.0.0.0", "0-0-0-0"])]
    [InlineData(["127.0.0.1", "127-0-0-1"])]
    public void SafeHost_ReplacesIpv4Dots_WithDashes(string ip, string expected)
    {
        string actual = ip.SafeHost();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(["2001:db8::1", "2001-db8--1"])]
    [InlineData(["::1", "--1"])]
    [InlineData(["fe80::1", "fe80--1"])]
    [InlineData(["2001:0db8:0000:0000:0000:0000:0000:0001", "2001-0db8-0000-0000-0000-0000-0000-0001"])]
    public void SafeHost_ReplacesIpv6Colons_WithDashes(string ipv6, string expected)
    {
        string actual = ipv6.SafeHost();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(["192.168.1.1", false])]
    [InlineData(["10.0.0.5", false])]
    [InlineData(["2001:db8::1", false])]
    public void SafeHost_ResultContainsNoDots_AndNoColons(string ip, bool _)
    {
        string actual = ip.SafeHost();

        Assert.DoesNotContain(".", actual);
        Assert.DoesNotContain(":", actual);
    }

    [Theory]
    [InlineData("192.168.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.31.255.255")]
    public void SafeHost_ProducesValidSubdomainSegment_ForIpv4(string ip)
    {
        string actual = ip.SafeHost();

        Assert.Matches(@"^[a-zA-Z0-9\-]+$", actual);
    }

    [Fact]
    public void SafeHost_EmptyString_ReturnsEmptyString()
    {
        string actual = string.Empty.SafeHost();

        Assert.Equal(string.Empty, actual);
    }

    [Fact]
    public void SafeHost_NoDotOrColon_ReturnsUnchanged()
    {
        const string input = "nomercy";

        string actual = input.SafeHost();

        Assert.Equal("nomercy", actual);
    }
}
