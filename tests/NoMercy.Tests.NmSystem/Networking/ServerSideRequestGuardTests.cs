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
using NoMercy.NmSystem.Networking;
using Xunit;

namespace NoMercy.Tests.NmSystem.Networking;

[Trait(name: "Category", value: "Unit")]
public class ServerSideRequestGuardTests
{
    [Theory]
    // Publicly routable — allowed.
    [InlineData(data: ["8.8.8.8", true])]
    [InlineData(data: ["1.1.1.1", true])]
    [InlineData(data: ["93.184.216.34", true])]
    [InlineData(data: ["2606:4700:4700::1111", true])]
    // Loopback.
    [InlineData(data: ["127.0.0.1", false])]
    [InlineData(data: ["127.255.255.254", false])]
    [InlineData(data: ["::1", false])]
    // RFC 1918 private.
    [InlineData(data: ["10.0.0.5", false])]
    [InlineData(data: ["172.16.0.1", false])]
    [InlineData(data: ["172.31.255.255", false])]
    [InlineData(data: ["192.168.1.10", false])]
    // Link-local (incl. the cloud metadata address) + CGNAT + unspecified.
    [InlineData(data: ["169.254.169.254", false])]
    [InlineData(data: ["100.64.0.1", false])]
    [InlineData(data: ["0.0.0.0", false])]
    // IPv6 link-local + unique-local.
    [InlineData(data: ["fe80::1", false])]
    [InlineData(data: ["fc00::1", false])]
    [InlineData(data: ["fd12:3456::1", false])]
    public void IsPubliclyRoutable_ClassifiesAddress(string ip, bool expected)
    {
        Assert.Equal(expected: expected, actual: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.Parse(ipString: ip)));
    }

    [Fact]
    public void IsPubliclyRoutable_IPv4MappedPrivateV6_IsRejected()
    {
        // ::ffff:10.0.0.1 must be unwrapped and rejected, not treated as a public v6.
        Assert.False(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.Parse(ipString: "::ffff:10.0.0.1")));
    }

    [Theory]
    // Absolute http(s) URL to a public IP literal — allowed (no DNS needed).
    [InlineData(data: ["https://1.1.1.1/openload.srt", true])]
    [InlineData(data: ["http://8.8.8.8/x", true])]
    // The SSRF payloads that motivated the guard.
    [InlineData(data: ["http://169.254.169.254/latest/meta-data/", false])]
    [InlineData(data: ["http://127.0.0.1:7626/api/v1/dashboard/server", false])]
    [InlineData(data: ["https://10.0.0.1/preset.json", false])]
    [InlineData(data: ["http://[::1]/x", false])]
    // Non-http(s) schemes and malformed input.
    [InlineData(data: ["file:///etc/passwd", false])]
    [InlineData(data: ["ftp://1.1.1.1/x", false])]
    [InlineData(data: ["gopher://1.1.1.1/x", false])]
    [InlineData(data: ["not-a-url", false])]
    [InlineData(data: ["/relative/path", false])]
    [InlineData(data: ["", false])]
    public async Task IsSafePublicHttpUrlAsync_ValidatesSchemeAndHost(string url, bool expected)
    {
        Assert.Equal(expected: expected, actual: await ServerSideRequestGuard.IsSafePublicHttpUrlAsync(url: url));
    }

    [Fact]
    public async Task IsSafePublicHttpUrlAsync_Null_IsRejected()
    {
        Assert.False(condition: await ServerSideRequestGuard.IsSafePublicHttpUrlAsync(url: null));
    }

    [Theory]
    [InlineData(data: "   ")]
    [InlineData(data: "\t")]
    [InlineData(data: "\n")]
    public async Task IsSafePublicHttpUrlAsync_WhitespaceOnly_IsRejected(string url)
    {
        Assert.False(condition: await ServerSideRequestGuard.IsSafePublicHttpUrlAsync(url: url));
    }

    [Theory]
    [InlineData(data: "172.20.0.1")]
    [InlineData(data: "172.31.0.1")]
    public void IsPubliclyRoutable_172RangePrivate_IsRejected(string ip)
    {
        Assert.False(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.Parse(ipString: ip)));
    }

    [Theory]
    [InlineData(data: "100.127.255.255")]
    [InlineData(data: "100.64.0.0")]
    public void IsPubliclyRoutable_CGNATRange_IsRejected(string ip)
    {
        Assert.False(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.Parse(ipString: ip)));
    }

    [Theory]
    [InlineData(data: "ff00::1")]
    [InlineData(data: "ff02::1")]
    public void IsPubliclyRoutable_IPv6Multicast_IsRejected(string ip)
    {
        Assert.False(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.Parse(ipString: ip)));
    }

    [Fact]
    public void IsPubliclyRoutable_IPv6Any_IsRejected()
    {
        Assert.False(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.IPv6Any));
    }

    [Fact]
    public void IsPubliclyRoutable_IPv6None_IsRejected()
    {
        Assert.False(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.IPv6None));
    }

    [Fact]
    public async Task IsSafePublicHttpUrlAsync_MixedCaseScheme_Accepted()
    {
        Assert.True(condition: await ServerSideRequestGuard.IsSafePublicHttpUrlAsync(url: "HTTPS://8.8.8.8/"));
    }

    [Fact]
    public async Task IsSafePublicHttpUrlAsync_WithPort_Accepted()
    {
        Assert.True(condition: await ServerSideRequestGuard.IsSafePublicHttpUrlAsync(url: "https://1.1.1.1:443/path"));
    }

    [Fact]
    public void IsPubliclyRoutable_PublicIpv6_Accepted()
    {
        Assert.True(condition: ServerSideRequestGuard.IsPubliclyRoutable(address: IPAddress.Parse(ipString: "2001:4860:4860::8888")));
    }
}
