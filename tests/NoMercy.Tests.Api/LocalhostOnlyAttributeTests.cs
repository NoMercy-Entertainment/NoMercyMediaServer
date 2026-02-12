using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NoMercy.Api.Middleware;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class LocalhostOnlyAttributeTests
{
    [Fact]
    public void OnAuthorization_LoopbackIp_AllowsRequest()
    {
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(IPAddress.Loopback);

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_IPv6Loopback_AllowsRequest()
    {
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(IPAddress.IPv6Loopback);

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_RemoteIp_BlocksRequest()
    {
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(IPAddress.Parse("192.168.1.100"));

        attribute.OnAuthorization(context);

        Assert.NotNull(context.Result);
        JsonResult jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(403, jsonResult.StatusCode);
    }

    [Fact]
    public void OnAuthorization_NullRemoteIp_AllowsRequest()
    {
        // Null remote IP happens with named pipes, IPC, and in-process test hosts.
        // The primary security boundary is Kestrel binding to 127.0.0.1 only.
        LocalhostOnlyAttribute attribute = new();
        AuthorizationFilterContext context = CreateContext(null);

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    private static AuthorizationFilterContext CreateContext(IPAddress? remoteIp)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Connection.RemoteIpAddress = remoteIp;

        ActionContext actionContext = new(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }
}
