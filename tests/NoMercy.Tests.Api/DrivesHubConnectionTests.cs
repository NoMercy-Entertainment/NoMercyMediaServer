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
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Networking.Messaging;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

// DrivesHub is broadcast-only: it exposes no client-invokable methods of its
// own and relies entirely on ConnectionHub.OnConnectedAsync/OnDisconnectedAsync
// for connection tracking and the "ConnectedDevicesState" broadcast that
// DriveMonitorEventHandler's drive-change pushes depend on. These tests build
// a real DrivesHub against the app's actual DI-configured MediaContext/UserCache
// (via NoMercyApiFactory), mocking only the SignalR plumbing (HubCallerContext,
// IHubCallerClients) a live connection would normally supply.
[Trait("Category", "Characterization")]
public class DrivesHubConnectionTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public DrivesHubConnectionTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        // Force the test host to start so UserCache.Current is populated with
        // the seeded DefaultUserId.
        _factory.CreateClient();
    }

    private DrivesHub CreateHub(
        ConnectedClients connectedClients,
        out Mock<ISingleClientProxy> userProxy
    )
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/drivesHub";

        DrivesHub hub = new(
            NullLogger<DrivesHub>.Instance,
            new HttpContextAccessorStub(httpContext),
            contextFactory,
            connectedClients,
            Mock.Of<IActivityLogger>()
        );

        ClaimsPrincipal principal = new(
            new ClaimsIdentity(
                [new(ClaimTypes.NameIdentifier, TestAuthHandler.DefaultUserId.ToString())],
                "TestAuth"
            )
        );

        Mock<HubCallerContext> context = new();
        context.Setup(c => c.User).Returns(principal);
        context.Setup(c => c.ConnectionId).Returns(Guid.NewGuid().ToString());
        context.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        userProxy = new Mock<ISingleClientProxy>();
        Mock<IHubCallerClients> clients = new();
        clients.Setup(c => c.User(It.IsAny<string>())).Returns(userProxy.Object);
        clients.Setup(c => c.Caller).Returns(Mock.Of<ISingleClientProxy>());

        hub.Context = context.Object;
        hub.Clients = clients.Object;

        return hub;
    }

    [Fact]
    public async Task OnConnectedAsync_RegistersCallerConnection_ForCachedUser()
    {
        ConnectedClients connectedClients = new();
        DrivesHub hub = CreateHub(connectedClients, out _);

        await hub.OnConnectedAsync();

        connectedClients.Clients.Should().ContainKey(hub.Context.ConnectionId);
        connectedClients
            .Clients[hub.Context.ConnectionId]
            .Sub.Should()
            .Be(TestAuthHandler.DefaultUserId);
    }

    [Fact]
    public async Task OnConnectedAsync_BroadcastsConnectedDevicesState_ToCallerUserGroup()
    {
        ConnectedClients connectedClients = new();
        DrivesHub hub = CreateHub(connectedClients, out Mock<ISingleClientProxy> userProxy);

        await hub.OnConnectedAsync();

        userProxy.Verify(
            p =>
                p.SendCoreAsync(
                    "ConnectedDevicesState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesCallerConnection_AfterConnect()
    {
        ConnectedClients connectedClients = new();
        DrivesHub hub = CreateHub(connectedClients, out _);
        await hub.OnConnectedAsync();
        string connectionId = hub.Context.ConnectionId;
        connectedClients.Clients.Should().ContainKey(connectionId);

        await hub.OnDisconnectedAsync(null);

        connectedClients.Clients.Should().NotContainKey(connectionId);
    }

    [Fact]
    public async Task OnDisconnectedAsync_IsNoOp_WhenConnectionWasNeverRegistered()
    {
        ConnectedClients connectedClients = new();
        DrivesHub hub = CreateHub(connectedClients, out Mock<ISingleClientProxy> userProxy);

        await hub.OnDisconnectedAsync(null);

        connectedClients.Clients.Should().BeEmpty();
        userProxy.Verify(
            p =>
                p.SendCoreAsync(
                    "ConnectedDevicesState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // Minimal IHttpContextAccessor stand-in — the real implementation is an
    // AsyncLocal-backed singleton unsuited to constructing an isolated
    // HttpContext per test.
    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
