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
using Microsoft.Extensions.Primitives;
using Moq;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Users;
using NoMercy.Networking;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.Tests.Networking.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: ConnectionHub is the base every SignalR hub in the server
/// inherits — its device-upsert-on-connect, device-inactive-on-disconnect,
/// and per-user Devices() filtering are relied on by every derived hub
/// (Video/Music/Dashboard/Cast/Ripper). A user with no cached DB record must
/// never be added to ConnectedClients (so no broadcast targets a ghost
/// connection); a connecting client_id must be upserted and preserve the
/// persisted CustomName/VolumePercent across reconnects; Devices() must only
/// ever return the CALLING user's OWN devices on THIS hub endpoint, never
/// another user's or another hub's connections.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class ConnectionHubTests : IDisposable
{
    private sealed class TestableConnectionHub : ConnectionHub
    {
        public TestableConnectionHub(
            IHttpContextAccessor httpContextAccessor,
            IDbContextFactory<MediaContext> contextFactory,
            ConnectedClients connectedClients,
            IActivityLogger activityLogger
        )
            : base(httpContextAccessor: httpContextAccessor, contextFactory: contextFactory, connectedClients: connectedClients, activityLogger: activityLogger) { }

        public IUserCache ExposedUserCacheService => UserCacheService;
        public IMediaAuthorizationPolicy ExposedAuthPolicy => AuthPolicy;
    }

    private readonly ConnectionHubTestDbContextFactory _disposableContextFactory = new();
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly ConnectedClients _connectedClients = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();

    public ConnectionHubTests()
    {
        _contextFactory = _disposableContextFactory;
        _activityLogger
            .Setup(expression: l =>
                l.LogConnectionAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Ulid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);
    }

    public void Dispose() => _disposableContextFactory.Dispose();

    private TestableConnectionHub BuildHub(
        User? cachedUser,
        out Mock<ISingleClientProxy> callerProxy,
        out Mock<ISingleClientProxy> userProxy,
        string path = "/videoHub",
        IQueryCollection? query = null,
        Guid? connectingUserId = null
    )
    {
        UserCache userCache = new();
        if (cachedUser is not null)
            userCache.AddUser(user: cachedUser);

        ServiceCollection services = new();
        services.AddSingleton<IUserCache>(implementationInstance: userCache);
        ServiceProvider provider = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() { RequestServices = provider };
        httpContext.Request.Path = path;
        if (query is not null)
            httpContext.Request.Query = query;
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipString: "10.0.0.7");

        TestableConnectionHub hub = new(
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: _contextFactory,
            connectedClients: _connectedClients,
            activityLogger: _activityLogger.Object
        );

        Guid userId = connectingUserId ?? cachedUser?.Id ?? Guid.NewGuid();
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())], authenticationType: "TestAuth")
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
    public async Task OnConnectedAsync_UserNotInCache_DoesNotAddToConnectedClients()
    {
        TestableConnectionHub hub = BuildHub(
            cachedUser: null,
            callerProxy: out _,
            userProxy: out _,
            connectingUserId: Guid.NewGuid()
        );

        await hub.OnConnectedAsync();

        Assert.Empty(collection: _connectedClients.Clients);
    }

    [Fact]
    public async Task OnConnectedAsync_UserInCache_AddsToConnectedClients()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-abc",
                [key: "client_name"] = "Living Room TV",
                [key: "client_type"] = "tv",
            }
        );

        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);

        await hub.OnConnectedAsync();

        Assert.Single(collection: _connectedClients.Clients);
        Client stored = _connectedClients.Clients.Values.Single();
        Assert.Equal(expected: user.Id, actual: stored.Sub);
        Assert.Equal(expected: "device-abc", actual: stored.DeviceId);
        Assert.Equal(expected: "Living Room TV", actual: stored.Name);
        Assert.True(condition: stored.IsActive);
    }

    [Fact]
    public async Task OnConnectedAsync_WithClientId_UpsertsDeviceRow()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-upsert-test",
                [key: "client_type"] = "tv",
            }
        );
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);

        await hub.OnConnectedAsync();

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FirstOrDefaultAsync(predicate: d =>
            d.DeviceId == "device-upsert-test"
        );
        Assert.NotNull(@object: device);
        Assert.True(condition: device!.IsActive);
    }

    [Fact]
    public async Task OnConnectedAsync_WithoutClientId_DoesNotTouchDatabase_ButStillTracksClient()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _);

        await hub.OnConnectedAsync();

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Assert.Equal(expected: 0, actual: await ctx.Devices.CountAsync());
        Assert.Single(collection: _connectedClients.Clients);
    }

    [Fact]
    public async Task OnConnectedAsync_Reconnect_PreservesPersistedCustomNameAndVolume()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };

        await using (MediaContext seed = await _contextFactory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: new()
                {
                    DeviceId = "device-reconnect",
                    Type = "tv",
                    CustomName = "Bedroom TV",
                    VolumePercent = 42,
                }
            );
            await seed.SaveChangesAsync();
        }

        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-reconnect",
                [key: "client_type"] = "tv",
                [key: "client_volume"] = "77", // must NOT clobber the persisted 42
            }
        );
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);

        await hub.OnConnectedAsync();

        Client stored = _connectedClients.Clients.Values.Single();
        Assert.Equal(expected: "Bedroom TV", actual: stored.CustomName);
        Assert.Equal(expected: 42, actual: stored.VolumePercent);
    }

    [Fact]
    public async Task OnConnectedAsync_NonEmptyCustomName_OverwritesPersistedName()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        await using (MediaContext seed = await _contextFactory.CreateDbContextAsync())
        {
            seed.Devices.Add(
                entity: new()
                {
                    DeviceId = "device-rename",
                    Type = "tv",
                    CustomName = "Old Name",
                }
            );
            await seed.SaveChangesAsync();
        }

        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-rename",
                [key: "client_type"] = "tv",
                [key: "custom_name"] = "New Name",
            }
        );
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);

        await hub.OnConnectedAsync();

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device device = await ctx.Devices.SingleAsync(predicate: d => d.DeviceId == "device-rename");
        Assert.Equal(expected: "New Name", actual: device.CustomName);
    }

    [Fact]
    public async Task OnConnectedAsync_VolumePercentQueryParam_IsClampedTo0To100()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-clamp",
                [key: "client_type"] = "tv",
                [key: "client_volume"] = "150",
            }
        );
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);

        await hub.OnConnectedAsync();

        // First-ever connection for this device — the upsert's INSERT path
        // (unlike WhenMatched on a reconnect) carries the whole in-memory
        // client, so the clamped value legitimately lands in the DB and
        // AlignClientWithPersistedDevice reads it straight back.
        Client stored = _connectedClients.Clients.Values.Single();
        Assert.Equal(expected: 100, actual: stored.VolumePercent);
    }

    [Fact]
    public async Task OnConnectedAsync_NegativeVolumePercentQueryParam_ClampsToZero()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-clamp-negative",
                [key: "client_type"] = "tv",
                [key: "client_volume"] = "-20",
            }
        );
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);

        await hub.OnConnectedAsync();

        Client stored = _connectedClients.Clients.Values.Single();
        Assert.Equal(expected: 0, actual: stored.VolumePercent);
    }

    [Fact]
    public async Task OnConnectedAsync_SendsConnectedDevicesState_ToCallingUserOnly()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        TestableConnectionHub hub = BuildHub(
            cachedUser: user,
            callerProxy: out Mock<ISingleClientProxy> callerProxy,
            userProxy: out Mock<ISingleClientProxy> userProxy
        );

        await hub.OnConnectedAsync();

        userProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "ConnectedDevicesState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
        callerProxy.Verify(
            expression: p =>
                p.SendCoreAsync(
                    "ConnectedDevicesState",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task OnDisconnectedAsync_UntrackedConnection_DoesNotThrow()
    {
        TestableConnectionHub hub = BuildHub(cachedUser: null, callerProxy: out _, userProxy: out _);

        Exception? ex = await Record.ExceptionAsync(testCode: () => hub.OnDisconnectedAsync(exception: null));

        Assert.Null(@object: ex);
        Assert.Empty(collection: _connectedClients.Clients);
    }

    [Fact]
    public async Task OnDisconnectedAsync_TrackedConnection_MarksDeviceInactive_AndRemovesFromMap()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        QueryCollection query = new(
            store: new Dictionary<string, StringValues>
            {
                [key: "client_id"] = "device-disconnect",
                [key: "client_type"] = "tv",
            }
        );
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, query: query);
        await hub.OnConnectedAsync();
        Assert.Single(collection: _connectedClients.Clients);

        await hub.OnDisconnectedAsync(exception: null);

        Assert.Empty(collection: _connectedClients.Clients);
        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device device = await ctx.Devices.SingleAsync(predicate: d => d.DeviceId == "device-disconnect");
        Assert.False(condition: device.IsActive);
    }

    [Fact]
    public void Devices_UserNotInCache_ReturnsEmptyList()
    {
        TestableConnectionHub hub = BuildHub(
            cachedUser: null,
            callerProxy: out _,
            userProxy: out _,
            connectingUserId: Guid.NewGuid()
        );

        List<Device> devices = hub.Devices();

        Assert.Empty(collection: devices);
    }

    [Fact]
    public async Task Devices_OnlyReturnsCallingUsersOwnConnections_OnThisEndpoint()
    {
        User userA = new()
        {
            Id = Guid.NewGuid(),
            Email = "a@nomercy.tv",
            Name = "A",
        };
        User userB = new()
        {
            Id = Guid.NewGuid(),
            Email = "b@nomercy.tv",
            Name = "B",
        };

        // Simulate userB already connected on the same endpoint.
        _connectedClients.Clients.TryAdd(
            key: "other-connection",
            value: new()
            {
                Sub = userB.Id,
                Endpoint = "/videoHub",
                DeviceId = "device-b",
                Name = "B's device",
            }
        );

        TestableConnectionHub hub = BuildHub(cachedUser: userA, callerProxy: out _, userProxy: out _, path: "/videoHub");
        await hub.OnConnectedAsync();

        List<Device> devices = hub.Devices();

        Assert.DoesNotContain(collection: devices, filter: d => d.DeviceId == "device-b");
    }

    [Fact]
    public async Task Devices_IgnoresConnectionsOnADifferentHubEndpoint()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };

        _connectedClients.Clients.TryAdd(
            key: "music-hub-connection",
            value: new()
            {
                Sub = user.Id,
                Endpoint = "/musicHub",
                DeviceId = "device-music",
                Name = "Music device",
            }
        );

        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _, path: "/videoHub");
        await hub.OnConnectedAsync();

        List<Device> devices = hub.Devices();

        Assert.DoesNotContain(collection: devices, filter: d => d.DeviceId == "device-music");
    }

    [Fact]
    public void GetCountryFromContext_NoHeader_DefaultsToUs()
    {
        TestableConnectionHub hub = BuildHub(cachedUser: null, callerProxy: out _, userProxy: out _);

        string country = hub.GetCountryFromContext();

        Assert.Equal(expected: "US", actual: country);
    }

    [Fact]
    public void GetCountryFromContext_HeaderPresent_ReturnsHeaderValue()
    {
        UserCache userCache = new();
        ServiceCollection services = new();
        services.AddSingleton<IUserCache>(implementationInstance: userCache);
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Request.Path = "/videoHub";
        httpContext.Request.Headers[key: "country"] = "NL";

        TestableConnectionHub hub = new(
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: _contextFactory,
            connectedClients: _connectedClients,
            activityLogger: _activityLogger.Object
        );

        string country = hub.GetCountryFromContext();

        Assert.Equal(expected: "NL", actual: country);
    }

    [Fact]
    public void GetLanguageFromContext_AcceptLanguageHeader_ReturnsPrimaryLanguageTag()
    {
        UserCache userCache = new();
        ServiceCollection services = new();
        services.AddSingleton<IUserCache>(implementationInstance: userCache);
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Request.Path = "/videoHub";
        httpContext.Request.Headers.AcceptLanguage = "nl_NL";

        TestableConnectionHub hub = new(
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: _contextFactory,
            connectedClients: _connectedClients,
            activityLogger: _activityLogger.Object
        );

        string? language = hub.GetLanguageFromContext();

        Assert.Equal(expected: "nl", actual: language);
    }

    [Fact]
    public void GetLanguageFromContext_NoHeader_FallsBackToGlobalLocalizerTargetLanguage()
    {
        TestableConnectionHub hub = BuildHub(cachedUser: null, callerProxy: out _, userProxy: out _);

        string? language = hub.GetLanguageFromContext();

        Assert.NotNull(@object: language);
    }

    [Fact]
    public void UserCacheService_WhenRequestServicesResolvesIt_UsesResolvedInstance()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        TestableConnectionHub hub = BuildHub(cachedUser: user, callerProxy: out _, userProxy: out _);

        IUserCache resolved = hub.ExposedUserCacheService;

        Assert.NotNull(@object: resolved.GetUser(userId: user.Id));
    }

    [Fact]
    public void AuthPolicy_WhenRequestServicesResolvesNoPolicy_FallsBackToDefault()
    {
        TestableConnectionHub hub = BuildHub(cachedUser: null, callerProxy: out _, userProxy: out _);

        IMediaAuthorizationPolicy policy = hub.ExposedAuthPolicy;

        Assert.NotNull(@object: policy);
    }

    // -- HttpContext entirely absent (e.g. a hub method invoked outside a
    // request-bound connection). Every one of these accessors must fall back
    // to a safe default instead of NRE-ing on a null HttpContext.

    private TestableConnectionHub BuildHubWithNullHttpContext()
    {
        HttpContextAccessorStub accessor = new(httpContext: null!);
        return new(httpContextAccessor: accessor, contextFactory: _contextFactory, connectedClients: _connectedClients, activityLogger: _activityLogger.Object);
    }

    [Fact]
    public void GetCountryFromContext_NullHttpContext_DefaultsToUs()
    {
        TestableConnectionHub hub = BuildHubWithNullHttpContext();

        string country = hub.GetCountryFromContext();

        Assert.Equal(expected: "US", actual: country);
    }

    [Fact]
    public void GetLanguageFromContext_NullHttpContext_FallsBackToGlobalLocalizer()
    {
        TestableConnectionHub hub = BuildHubWithNullHttpContext();

        string? language = hub.GetLanguageFromContext();

        Assert.NotNull(@object: language);
    }

    [Fact]
    public void UserCacheService_NullHttpContext_FallsBackToUserCacheCurrent()
    {
        TestableConnectionHub hub = BuildHubWithNullHttpContext();

        IUserCache resolved = hub.ExposedUserCacheService;

        Assert.Same(expected: UserCache.Current, actual: resolved);
    }

    [Fact]
    public void AuthPolicy_NullHttpContext_FallsBackToDefault()
    {
        TestableConnectionHub hub = BuildHubWithNullHttpContext();

        IMediaAuthorizationPolicy policy = hub.ExposedAuthPolicy;

        Assert.NotNull(@object: policy);
    }

    [Fact]
    public void UserCacheService_HttpContextPresent_ButRequestServicesNull_FallsBackToUserCacheCurrent()
    {
        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/videoHub";
        TestableConnectionHub hub = new(
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: _contextFactory,
            connectedClients: _connectedClients,
            activityLogger: _activityLogger.Object
        );

        IUserCache resolved = hub.ExposedUserCacheService;

        Assert.Same(expected: UserCache.Current, actual: resolved);
    }

    [Fact]
    public void AuthPolicy_HttpContextPresent_ButRequestServicesNull_FallsBackToDefault()
    {
        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/videoHub";
        TestableConnectionHub hub = new(
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: _contextFactory,
            connectedClients: _connectedClients,
            activityLogger: _activityLogger.Object
        );

        IMediaAuthorizationPolicy policy = hub.ExposedAuthPolicy;

        Assert.NotNull(@object: policy);
    }

    [Fact]
    public async Task OnConnectedAsync_NullHttpContext_EndpointFallsBackToUnknown()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = "test@nomercy.tv",
            Name = "Test",
        };
        UserCache userCache = new();
        userCache.AddUser(user: user);

        HttpContextAccessorStub accessor = new(httpContext: null!);
        TestableConnectionHub hub = new(
            httpContextAccessor: accessor,
            contextFactory: _contextFactory,
            connectedClients: _connectedClients,
            activityLogger: _activityLogger.Object
        );

        Mock<HubCallerContext> context = new();
        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: user.Id.ToString())], authenticationType: "TestAuth")
        );
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: Guid.NewGuid().ToString());
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);
        hub.Context = context.Object;

        Mock<IHubCallerClients> clients = new();
        clients.Setup(expression: c => c.Caller).Returns(value: Mock.Of<ISingleClientProxy>());
        clients.Setup(expression: c => c.User(It.IsAny<string>())).Returns(value: Mock.Of<ISingleClientProxy>());
        hub.Clients = clients.Object;

        // UserCacheService falls back to the shared UserCache.Current when
        // HttpContext is null, so this test seeds the calling user there
        // instead of a per-test DI container. Reset afterward to keep this
        // static, process-wide cache from leaking into other tests.
        UserCache.Current.AddUser(user: user);
        try
        {
            await hub.OnConnectedAsync();
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
        }

        Client stored = _connectedClients.Clients.Values.Single();
        Assert.Equal(expected: "Unknown", actual: stored.Endpoint);
    }
}
