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

using Microsoft.AspNetCore.Http;
using NoMercy.Api.Middleware;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

public sealed class PrivateNetworkAccessMiddlewareTests
{
    private static async Task<HttpContext> Invoke(string method, string? requestPrivateNetwork)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        if (requestPrivateNetwork is not null)
            context.Request.Headers[key: "Access-Control-Request-Private-Network"] =
                requestPrivateNetwork;

        PrivateNetworkAccessMiddleware middleware = new(next: _ => Task.CompletedTask);
        await middleware.InvokeAsync(context: context);
        return context;
    }

    [Fact]
    public async Task Preflight_RequestingPrivateNetwork_GetsAllowHeader()
    {
        HttpContext context = await Invoke(method: "OPTIONS", requestPrivateNetwork: "true");

        context
            .Response.Headers[key: "Access-Control-Allow-Private-Network"]
            .ToString()
            .Should()
            .Be(expected: "true");
    }

    [Fact]
    public async Task Preflight_WithoutPrivateNetworkRequest_HasNoAllowHeader()
    {
        HttpContext context = await Invoke(method: "OPTIONS", requestPrivateNetwork: null);

        context
            .Response.Headers.ContainsKey(key: "Access-Control-Allow-Private-Network")
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task NonPreflightRequest_IsNeverAnnotated()
    {
        // A real GET carrying the request header (spoofed or stale) must not draw
        // the opt-in — the header only belongs on an actual preflight.
        HttpContext context = await Invoke(method: "GET", requestPrivateNetwork: "true");

        context
            .Response.Headers.ContainsKey(key: "Access-Control-Allow-Private-Network")
            .Should()
            .BeFalse();
    }
}
