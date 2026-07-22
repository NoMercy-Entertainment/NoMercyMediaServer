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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Hubs;
using NoMercy.Api.WebSockets;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

// StartResources()/StopResources() used to be reachable by any authenticated
// dashboardHub client — a merely-allowed, non-moderator user could toggle the
// shared resource-monitor and log-broadcast services for every connected
// dashboard client. Both methods now require the same "moderators" tier the
// hub already grants group membership to on connect (DashboardHub.cs ~line
// 52). These tests build a real DashboardHub against the app's actual
// DI-configured MediaContext/UserCache (via NoMercyApiFactory) and mock only
// the SignalR plumbing (HubCallerContext) plus the two gated services.
[Trait(name: "Category", value: "Characterization")]
public class DashboardHubResourceAuthTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public DashboardHubResourceAuthTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        // Force the test host to start so UserCache.Current is populated with
        // the seeded DefaultUserId (Owner=true, Manage=true) and SecondaryUserId
        // (Allowed=true, Owner=false, Manage=false) test users.
        _factory.CreateClient();
    }

    private DashboardHub CreateHub(
        Guid callerUserId,
        out Mock<IResourceMonitorService> resourceMonitorService,
        out Mock<ILogBroadcastService> logBroadcastService
    )
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/dashboardHub";

        resourceMonitorService = new();
        logBroadcastService = new();

        DashboardHub hub = new(
            logger: NullLogger<DashboardHub>.Instance,
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: contextFactory,
            // A fresh, empty ConnectedClients so StopResources' "last dashboard
            // connection" check always evaluates true, isolated from any other
            // test's device state.
            connectedClients: new ConnectedClients(),
            clientMessenger: Mock.Of<IClientMessenger>(),
            logBroadcastService: logBroadcastService.Object,
            resourceMonitorService: resourceMonitorService.Object,
            activityLogger: Mock.Of<IActivityLogger>()
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(
                claims: [new(type: ClaimTypes.NameIdentifier, value: callerUserId.ToString())],
                authenticationType: "TestAuth"
            )
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: Guid.NewGuid().ToString());
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

        hub.Context = context.Object;
        hub.Clients = Mock.Of<IHubCallerClients>();

        return hub;
    }

    [Fact]
    public void StartResources_StartsMonitor_WhenCallerIsModerator()
    {
        DashboardHub hub = CreateHub(
            callerUserId: TestAuthHandler.DefaultUserId,
            resourceMonitorService: out Mock<IResourceMonitorService> resourceMonitorService,
            logBroadcastService: out _
        );

        hub.StartResources();

        resourceMonitorService.Verify(expression: s => s.Start(), times: Times.Once);
    }

    [Fact]
    public void StartResources_DoesNotStartMonitor_WhenCallerIsNonModeratorSecondaryUser()
    {
        // SecondaryUserId: Allowed=true, Owner=false, Manage=false — the seeded
        // "merely authenticated" tier that must no longer be able to start the
        // shared resource monitor for every connected dashboard client.
        DashboardHub hub = CreateHub(
            callerUserId: TestAuthHandler.SecondaryUserId,
            resourceMonitorService: out Mock<IResourceMonitorService> resourceMonitorService,
            logBroadcastService: out _
        );

        hub.StartResources();

        resourceMonitorService.Verify(expression: s => s.Start(), times: Times.Never);
    }

    [Fact]
    public void StopResources_StopsServices_WhenCallerIsModerator()
    {
        DashboardHub hub = CreateHub(
            callerUserId: TestAuthHandler.DefaultUserId,
            resourceMonitorService: out Mock<IResourceMonitorService> resourceMonitorService,
            logBroadcastService: out Mock<ILogBroadcastService> logBroadcastService
        );

        hub.StopResources();

        resourceMonitorService.Verify(expression: s => s.Stop(), times: Times.Once);
        logBroadcastService.Verify(expression: s => s.Stop(), times: Times.Once);
    }

    [Fact]
    public void StopResources_DoesNotStopServices_WhenCallerIsNonModeratorSecondaryUser()
    {
        DashboardHub hub = CreateHub(
            callerUserId: TestAuthHandler.SecondaryUserId,
            resourceMonitorService: out Mock<IResourceMonitorService> resourceMonitorService,
            logBroadcastService: out Mock<ILogBroadcastService> logBroadcastService
        );

        hub.StopResources();

        resourceMonitorService.Verify(expression: s => s.Stop(), times: Times.Never);
        logBroadcastService.Verify(expression: s => s.Stop(), times: Times.Never);
    }

    // Minimal IHttpContextAccessor stand-in — the real implementation is an
    // AsyncLocal-backed singleton unsuited to constructing an isolated
    // HttpContext per test.
    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
