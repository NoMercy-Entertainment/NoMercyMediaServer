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
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Devices;
using NoMercy.Networking.Devices;
using NoMercy.Networking.Messaging;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

// Regression coverage for the live two-TV verification finding: a TV connecting
// to /deviceHub only refreshed its OWN picker (Clients.Caller), so every other
// already-connected device's picker never learned about it until it happened to
// reconnect itself. The fix broadcasts "DeviceListChanged" to the whole user
// group instead. These tests build a real DeviceHub against the app's actual
// DI-configured MediaContext (via NoMercyApiFactory) and mock only the SignalR
// plumbing (HubCallerContext, IHubCallerClients) that a live connection would
// normally supply.
[Trait(name: "Category", value: "Characterization")]
public class DeviceHubBroadcastTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public DeviceHubBroadcastTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        // Force the test host to start so its root service provider is populated.
        _factory.CreateClient();
    }

    private async Task<(Ulid deviceA, Ulid deviceB)> SeedTwoOwnedDevicesAsync(
        IDbContextFactory<MediaContext> contextFactory
    )
    {
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();

        Device deviceA = new()
        {
            Id = Ulid.NewUlid(),
            DeviceId = $"tv-a-{Guid.NewGuid()}",
            Name = "Living Room TV",
            Type = "tv",
            Fingerprint = $"fp-a-{Guid.NewGuid()}",
            OwnerUserId = TestAuthHandler.DefaultUserId,
        };
        Device deviceB = new()
        {
            Id = Ulid.NewUlid(),
            DeviceId = $"tv-b-{Guid.NewGuid()}",
            Name = "Bedroom TV",
            Type = "tv",
            Fingerprint = $"fp-b-{Guid.NewGuid()}",
            OwnerUserId = TestAuthHandler.DefaultUserId,
        };
        ctx.Devices.AddRange(entities: [deviceA, deviceB]);
        await ctx.SaveChangesAsync();

        return (deviceA.Id, deviceB.Id);
    }

    private DeviceHub CreateHub(
        out Mock<ISingleClientProxy> callerProxy,
        out Mock<ISingleClientProxy> userProxy
    )
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        DeviceBusRegistry busRegistry = new(contextFactory: contextFactory, hubContext: Mock.Of<IHubContext<DeviceHub>>());

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/deviceHub";

        DeviceHub hub = new(
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: contextFactory,
            connectedClients: connectedClients,
            busRegistry: busRegistry,
            activityLogger: Mock.Of<Database.Activity.IActivityLogger>(),
            capabilityRegistry: Mock.Of<IDeviceCapabilityRegistry>(),
            logger: NullLogger<DeviceHub>.Instance
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(
                claims: [new(type: ClaimTypes.NameIdentifier, value: TestAuthHandler.DefaultUserId.ToString())],
                authenticationType: "TestAuth"
            )
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: Guid.NewGuid().ToString());
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

        callerProxy = new();
        userProxy = new();

        Mock<IHubCallerClients> clients = new();
        clients.Setup(expression: c => c.Caller).Returns(value: callerProxy.Object);
        clients.Setup(expression: c => c.User(It.IsAny<string>())).Returns(value: userProxy.Object);

        hub.Context = context.Object;
        hub.Clients = clients.Object;

        return hub;
    }

    [Fact]
    public async Task OnConnectedAsync_BroadcastsDeviceListChanged_ToWholeUserGroup_NotJustCaller()
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await SeedTwoOwnedDevicesAsync(contextFactory: contextFactory);

        DeviceHub hub = CreateHub(
            callerProxy: out Mock<ISingleClientProxy> callerProxy,
            userProxy: out Mock<ISingleClientProxy> userProxy
        );

        await hub.OnConnectedAsync();

        // The whole user group must receive the refreshed list...
        userProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "DeviceListChanged",
                    It.Is<object?[]>(args => args.Length == 1 && args[0] is List<DeviceListItem>),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );

        // ...never just the connecting client. This is the regression: the old
        // code sent "DeviceListChanged" only to Clients.Caller, so every other
        // already-connected device's picker stayed stale.
        callerProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "DeviceListChanged",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task OnConnectedAsync_DeviceListChanged_ContainsBothOwnedDevices()
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        (Ulid deviceA, Ulid deviceB) = await SeedTwoOwnedDevicesAsync(contextFactory: contextFactory);

        DeviceHub hub = CreateHub(callerProxy: out _, userProxy: out Mock<ISingleClientProxy> userProxy);

        List<DeviceListItem>? broadcastList = null;
        userProxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    "DeviceListChanged",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, object?[], CancellationToken>(
                action: (_, args, _) => broadcastList = args[0] as List<DeviceListItem>
            )
            .Returns(value: Task.CompletedTask);

        await hub.OnConnectedAsync();

        broadcastList.Should().NotBeNull();
        broadcastList!.Select(selector: d => d.DeviceId).Should().Contain(expected: [deviceA, deviceB]);
    }

    [Fact]
    public async Task OnDisconnectedAsync_BroadcastsDeviceListChanged_ToWholeUserGroup()
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await SeedTwoOwnedDevicesAsync(contextFactory: contextFactory);

        DeviceHub hub = CreateHub(
            callerProxy: out Mock<ISingleClientProxy> callerProxy,
            userProxy: out Mock<ISingleClientProxy> userProxy
        );

        await hub.OnDisconnectedAsync(exception: null);

        userProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "DeviceListChanged",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );

        callerProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "DeviceListChanged",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
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
