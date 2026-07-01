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

namespace NoMercy.Tests.NmSystem;

[Trait("Category", "Unit")]
public class UrlExtensionsTests
{
    [Fact]
    public void ToHttps_ConvertHttpToHttps()
    {
        Uri url = new("http://example.com/path");
        Uri result = url.ToHttps();
        result.Scheme.Should().Be("https");
        result.Host.Should().Be("example.com");
        result.AbsolutePath.Should().Be("/path");
    }

    [Fact]
    public void ToHttps_AlreadyHttps_RemainsHttps()
    {
        Uri url = new("https://example.com/path");
        Uri result = url.ToHttps();
        result.Scheme.Should().Be("https");
    }

    [Fact]
    public void ToHttps_RemovesNonDefaultPort()
    {
        Uri url = new("http://example.com:8080/path");
        Uri result = url.ToHttps();
        result.Port.Should().Be(443);
    }

    [Theory]
    [InlineData("https://example.com/path/file.jpg", "file.jpg")]
    [InlineData("https://example.com/document.pdf", "document.pdf")]
    [InlineData("https://example.com/path/to/image.png", "image.png")]
    public void FileName_ExtractsFileNameFromUrl(string urlString, string expected)
    {
        Uri url = new(urlString);
        string result = url.FileName();
        result.Should().Be(expected);
    }

    [Fact]
    public void FileName_WithoutFile_ReturnsEmpty()
    {
        Uri url = new("https://example.com/path/");
        string result = url.FileName();
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://example.com/path/file.jpg", "https://example.com/path")]
    [InlineData("https://example.com/document.pdf", "https://example.com")]
    [InlineData("https://example.com/a/b/c/file.txt", "https://example.com/a/b/c")]
    public void BasePath_ExtractsUrlWithoutFileName(string urlString, string expected)
    {
        Uri url = new(urlString);
        string result = url.BasePath();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    public void SafeHost_ReplacesDotInIpv4(string ipAddress)
    {
        string result = ipAddress.SafeHost();
        result.Should().NotContain(".");
        result.Should().Contain("-");
    }

    [Theory]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("fe80::1")]
    [InlineData("::1")]
    public void SafeHost_ReplacesColonInIpv6(string ipAddress)
    {
        string result = ipAddress.SafeHost();
        result.Should().NotContain(":");
        result.Should().Contain("-");
    }

    [Fact]
    public void SafeHost_WithHostname_ReplacesDots()
    {
        string result = "example.com".SafeHost();
        result.Should().Be("example-com");
    }

    [Fact]
    public void SafeHost_WithMixedHostAndIp_OnlyReplacesIpParts()
    {
        string result = "server.192.168.1.1".SafeHost();
        result.Should().Contain("server");
        result.Should().NotContain(".1");
    }
}
