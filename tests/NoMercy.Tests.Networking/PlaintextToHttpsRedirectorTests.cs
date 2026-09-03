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

using System.Buffers;
using System.Text;
using NoMercy.Networking.Certificate;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait("Category", "Unit")]
public sealed class PlaintextToHttpsRedirectorTests
{
    private static ReadOnlySequence<byte> Request(string raw) =>
        new(Encoding.ASCII.GetBytes(raw));

    [Fact]
    public void ParseHostHeader_ReturnsHost_WithoutPlaintextPort()
    {
        ReadOnlySequence<byte> buffer = Request(
            "GET / HTTP/1.1\r\nHost: 10.0.1.1:7626\r\nConnection: keep-alive\r\n\r\n"
        );

        string? host = PlaintextToHttpsRedirector.ParseHostHeader(buffer);

        Assert.Equal("10.0.1.1", host);
    }

    [Fact]
    public void ParseHostHeader_ReturnsSynthesizedDnsHost_Unmodified()
    {
        ReadOnlySequence<byte> buffer = Request(
            "GET /dashboard HTTP/1.1\r\n"
                + "Host: 10-0-1-1.00380881-eb96-44e9-8e59-1258568d1a1f.srv.nomercy.tv:7626\r\n"
                + "\r\n"
        );

        string? host = PlaintextToHttpsRedirector.ParseHostHeader(buffer);

        Assert.Equal("10-0-1-1.00380881-eb96-44e9-8e59-1258568d1a1f.srv.nomercy.tv", host);
    }

    [Fact]
    public void ParseHostHeader_ReturnsNull_WhenHeaderMissing()
    {
        ReadOnlySequence<byte> buffer = Request("GET / HTTP/1.1\r\nConnection: keep-alive\r\n\r\n");

        string? host = PlaintextToHttpsRedirector.ParseHostHeader(buffer);

        Assert.Null(host);
    }

    [Fact]
    public void ParseRequestPath_ReturnsPathWithQueryString()
    {
        ReadOnlySequence<byte> buffer = Request(
            "GET /api/v1/search?q=matrix HTTP/1.1\r\nHost: 10.0.1.1:7626\r\n\r\n"
        );

        string path = PlaintextToHttpsRedirector.ParseRequestPath(buffer);

        Assert.Equal("/api/v1/search?q=matrix", path);
    }

    [Fact]
    public void ParseRequestPath_FallsBackToRoot_WhenRequestLineIsMalformed()
    {
        ReadOnlySequence<byte> buffer = Request("not an http request at all");

        string path = PlaintextToHttpsRedirector.ParseRequestPath(buffer);

        Assert.Equal("/", path);
    }
}
