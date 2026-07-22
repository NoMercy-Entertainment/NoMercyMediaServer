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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NoMercy.Api.Middleware;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Unit")]
public class LocalhostOnlyAttributeTests
{
    [Fact]
    public void OnAuthorization_LoopbackIp_AllowsRequest()
    {
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(remoteIp: IPAddress.Loopback);

        attribute.OnAuthorization(context: context);

        Assert.Null(@object: context.Result);
    }

    [Fact]
    public void OnAuthorization_IPv6Loopback_AllowsRequest()
    {
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(remoteIp: IPAddress.IPv6Loopback);

        attribute.OnAuthorization(context: context);

        Assert.Null(@object: context.Result);
    }

    [Fact]
    public void OnAuthorization_RemoteIp_BlocksRequest()
    {
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(remoteIp: IPAddress.Parse(ipString: "192.168.1.100"));

        attribute.OnAuthorization(context: context);

        Assert.NotNull(@object: context.Result);
        JsonResult jsonResult = Assert.IsType<JsonResult>(@object: context.Result);
        Assert.Equal(expected: 403, actual: jsonResult.StatusCode);
    }

    [Fact]
    public void OnAuthorization_NullRemoteIp_AllowsRequest()
    {
        // Null remote IP happens with named pipes, IPC, and in-process test hosts.
        // The primary security boundary is Kestrel binding to 127.0.0.1 only.
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(remoteIp: null);

        attribute.OnAuthorization(context: context);

        Assert.Null(@object: context.Result);
    }

    private static AuthorizationFilterContext CreateContext(IPAddress? remoteIp)
    {
        DefaultHttpContext httpContext = new() { Connection = { RemoteIpAddress = remoteIp } };

        ActionContext actionContext = new(httpContext: httpContext, routeData: new(), actionDescriptor: new());

        return new(actionContext: actionContext, filters: []);
    }
}
