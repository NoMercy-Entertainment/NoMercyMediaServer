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
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Hubs;
using NoMercy.Api.Services.Music;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Data.Activity;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Auth;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Cast;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

// Regression coverage for two live incidents on the same OnDisconnectedAsync path:
//
// 1) A music session sat PAUSED with its active device set to a TV whose app
//    process was killed. The liveness sweep (MusicPlaybackService.IsActiveDeviceStale)
//    only acts while PlayState is playing, so a wedged paused session survived
//    forever and every command from other devices relayed into a void. Fixed by
//    having OnDisconnectedAsync release (or end) the session the moment the
//    connection belonging to the CURRENT active device actually disconnects,
//    regardless of play state.
//
// 2) That first fix over-corrected: it ended the session outright (item:null)
//    whenever the disconnecting active device had been paused, even when OTHER
//    devices were still connected and could have resumed the exact spot it left
//    off. A user switching from a paused TV to a phone saw the whole session
//    vanish — the KMP client renders a null CurrentItem as a hard stop, not a
//    pause. The fix: graceful release (keep CurrentItem/Playlist/Backlog, just
//    clear PlayState + DeviceId) applies whenever ANY other device remains
//    connected, whether the vanished device was playing or paused. Item:null
//    end-of-session is now reserved for the case no device remains at all
//    (connectedDevices.Count == 0), which is a separate branch entirely.
//
// These tests build a real MusicHub against the app's actual DI-configured
// singletons (MusicPlayerStateManager, MusicActiveDeviceRegistry, ConnectedClients)
// via NoMercyApiFactory, mocking only the SignalR plumbing a live connection would
// normally supply.
[Trait(name: "Category", value: "Characterization")]
public class MusicHubActiveDeviceDisconnectTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public MusicHubActiveDeviceDisconnectTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        // Force the test host to start so its root service provider is populated.
        _factory.CreateClient();
    }

    private static PlaylistTrackDto MakeTrack()
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Track",
            Duration = "180",
            Filename = "test.mp3",
            Folder = "/music/",
            FolderId = Ulid.NewUlid(),
        };
        return new(track: track, country: "US");
    }

    private static Client MakeClient(Guid userId, string deviceId, string type = "web")
    {
        return new()
        {
            Id = Ulid.NewUlid(),
            Sub = userId,
            DeviceId = deviceId,
            Endpoint = "/musicHub",
            Type = type,
            Socket = Mock.Of<ISingleClientProxy>(),
        };
    }

    private MusicHub CreateHub(string connectionId, Guid userId)
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        IClientMessenger clientMessenger = _factory.Services.GetRequiredService<IClientMessenger>();
        MusicPlaybackService musicPlaybackService =
            _factory.Services.GetRequiredService<MusicPlaybackService>();
        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicPlaybackCommandHandler commandHandler =
            _factory.Services.GetRequiredService<MusicPlaybackCommandHandler>();
        MusicActiveDeviceRegistry activeDeviceRegistry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();
        CastPanelWakeLauncher castPanelWakeLauncher =
            _factory.Services.GetRequiredService<CastPanelWakeLauncher>();
        AuthManager authManager = _factory.Services.GetRequiredService<AuthManager>();

        MusicDeviceManager musicDeviceManager = new(mediaContext: new());
        MusicPlaylistManager musicPlaylistManager = new(musicService: new MusicRepository(contextFactory: contextFactory), mediaContext: new());
        DeviceBusRegistry busRegistry = new(contextFactory: contextFactory, hubContext: Mock.Of<IHubContext<DeviceHub>>());
        CastSessionTokenService castTokenService = new(authManager: authManager, authTokenStore: new AuthTokenStore());

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/musicHub";

        MusicHub hub = new(
            logger: NullLogger<MusicHub>.Instance,
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: contextFactory,
            connectedClients: connectedClients,
            clientMessenger: clientMessenger,
            musicPlaybackService: musicPlaybackService,
            musicPlayerStateManager: stateManager,
            musicDeviceManager: musicDeviceManager,
            musicPlaylistManager: musicPlaylistManager,
            commandHandler: commandHandler,
            activityLogger: Mock.Of<IActivityLogger>(),
            busRegistry: busRegistry,
            castTokenService: castTokenService,
            chromeCast: Mock.Of<IChromeCastService>(),
            castPanelWakeLauncher: castPanelWakeLauncher,
            activeDeviceRegistry: activeDeviceRegistry
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())], authenticationType: "TestAuth")
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: connectionId);
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

        Mock<ISingleClientProxy> callerProxy = new();
        Mock<ISingleClientProxy> userProxy = new();

        Mock<IHubCallerClients> clients = new();
        clients.Setup(expression: c => c.Caller).Returns(value: callerProxy.Object);
        clients.Setup(expression: c => c.User(It.IsAny<string>())).Returns(value: userProxy.Object);

        hub.Context = context.Object;
        hub.Clients = clients.Object;

        return hub;
    }

    private static User SeedTestUser(Guid userId)
    {
        User user = new()
        {
            Id = userId,
            Email = $"{userId}@nomercy.tv",
            Name = "Disconnect Test User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };
        UserCache.Current.AddUser(user: user);
        return user;
    }

    [Fact]
    public async Task LastConnectionOfActiveDevice_Disconnects_WhilePaused_OtherDeviceConnected_ReleasesActive_ButSessionSurvives()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client phoneClient = MakeClient(userId: userId, deviceId: phoneDeviceId);
        Client tvClient = MakeClient(userId: userId, deviceId: tvDeviceId, type: "tv");
        connectedClients.Clients[key: phoneConnectionId] = phoneClient;
        connectedClients.Clients[key: tvConnectionId] = tvClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        PlaylistTrackDto currentTrack = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = tvDeviceId,
            PlayState = false, // paused when the TV vanished
            CurrentItem = currentTrack,
            Playlist = [MakeTrack()],
            Backlog = [MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            MusicHub hub = CreateHub(connectionId: tvConnectionId, userId: userId);

            await hub.OnDisconnectedAsync(exception: null);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            // Graceful release: the phone is still connected and could resume this
            // exact spot, so the session must survive — a paused active device
            // vanishing must not read as "stop" on every remaining device.
            after!.CurrentItem.Should().Be(expected: currentTrack);
            after.Playlist.Should().HaveCount(expected: 1);
            after.Backlog.Should().HaveCount(expected: 1);
            after.PlayState.Should().BeFalse();
            after.DeviceId.Should().BeNull();
            after.Actions.Disallows.Resuming.Should().BeFalse();
            after.Actions.Disallows.Pausing.Should().BeTrue();

            // The dead TV must no longer be recorded as active — the very next
            // claim from any device (including the still-connected phone) must
            // be free to become active.
            registry.TryGet(userId: userId, device: out Device? active).Should().BeFalse();
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: phoneConnectionId, value: out _);
            connectedClients.Clients.TryRemove(key: tvConnectionId, value: out _);
            stateManager.RemoveState(userId: userId);
            registry.Remove(userId: userId);
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Fact]
    public async Task LastConnectionOfActiveDevice_Disconnects_WhilePaused_NoOtherDevicesConnected_EndsSessionCleanly()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string tvConnectionId = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client tvClient = MakeClient(userId: userId, deviceId: tvDeviceId, type: "tv");
        connectedClients.Clients[key: tvConnectionId] = tvClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        MusicPlayerState state = new()
        {
            DeviceId = tvDeviceId,
            PlayState = false, // paused when the TV vanished — the live incident
            CurrentItem = MakeTrack(),
            Playlist = [MakeTrack()],
            Backlog = [MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            // No other device connected — this is the ONLY live connection for
            // this user, so the connectedDevices.Count == 0 branch owns teardown.
            MusicHub hub = CreateHub(connectionId: tvConnectionId, userId: userId);

            await hub.OnDisconnectedAsync(exception: null);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeFalse();
            after.Should().BeNull();

            registry.TryGet(userId: userId, device: out Device? active).Should().BeFalse();
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: tvConnectionId, value: out _);
            stateManager.RemoveState(userId: userId);
            registry.Remove(userId: userId);
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Fact]
    public async Task LastConnectionOfActiveDevice_Disconnects_WhilePlaying_ReleasesActive_ButSessionSurvives()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client phoneClient = MakeClient(userId: userId, deviceId: phoneDeviceId);
        Client tvClient = MakeClient(userId: userId, deviceId: tvDeviceId, type: "tv");
        connectedClients.Clients[key: phoneConnectionId] = phoneClient;
        connectedClients.Clients[key: tvConnectionId] = tvClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        PlaylistTrackDto currentTrack = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = tvDeviceId,
            PlayState = true, // actively playing when the TV vanished
            CurrentItem = currentTrack,
            Playlist = [MakeTrack(), MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            MusicHub hub = CreateHub(connectionId: tvConnectionId, userId: userId);

            await hub.OnDisconnectedAsync(exception: null);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            // Graceful release: the session survives...
            after!.CurrentItem.Should().Be(expected: currentTrack);
            after.Playlist.Should().HaveCount(expected: 2);
            // ...but nobody owns it anymore, and PlayState reflects that nothing
            // is actually producing audio right now.
            after.PlayState.Should().BeFalse();
            after.DeviceId.Should().BeNull();
            after.Actions.Disallows.Resuming.Should().BeFalse();

            registry.TryGet(userId: userId, device: out Device? active).Should().BeFalse();
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: phoneConnectionId, value: out _);
            connectedClients.Clients.TryRemove(key: tvConnectionId, value: out _);
            stateManager.RemoveState(userId: userId);
            registry.Remove(userId: userId);
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Fact]
    public async Task NonActiveDeviceDisconnects_LeavesActiveSessionUntouched()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client phoneClient = MakeClient(userId: userId, deviceId: phoneDeviceId);
        Client tvClient = MakeClient(userId: userId, deviceId: tvDeviceId, type: "tv");
        connectedClients.Clients[key: phoneConnectionId] = phoneClient;
        connectedClients.Clients[key: tvConnectionId] = tvClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        PlaylistTrackDto currentTrack = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = tvDeviceId, // TV is active, not the disconnecting phone
            PlayState = true,
            CurrentItem = currentTrack,
            Playlist = [MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            // The PASSIVE phone disconnects, not the active TV.
            MusicHub hub = CreateHub(connectionId: phoneConnectionId, userId: userId);

            await hub.OnDisconnectedAsync(exception: null);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(expected: tvDeviceId);
            after.PlayState.Should().BeTrue();
            after.CurrentItem.Should().Be(expected: currentTrack);

            registry.TryGet(userId: userId, device: out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(expected: tvDeviceId);
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: phoneConnectionId, value: out _);
            connectedClients.Clients.TryRemove(key: tvConnectionId, value: out _);
            stateManager.RemoveState(userId: userId);
            registry.Remove(userId: userId);
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Fact]
    public async Task OneOfTwoConnectionsForSameActiveDevice_Drops_LeavesSessionUntouched()
    {
        // Simulates the KMP double-connect: the same device_id briefly holds two
        // live hub connections. Tearing down one must never release the device
        // while its other connection is still live.
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string tvConnectionId1 = Guid.NewGuid().ToString();
        string tvConnectionId2 = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client tvClient1 = MakeClient(userId: userId, deviceId: tvDeviceId, type: "tv");
        Client tvClient2 = MakeClient(userId: userId, deviceId: tvDeviceId, type: "tv");
        connectedClients.Clients[key: tvConnectionId1] = tvClient1;
        connectedClients.Clients[key: tvConnectionId2] = tvClient2;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        PlaylistTrackDto currentTrack = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = tvDeviceId,
            PlayState = true,
            CurrentItem = currentTrack,
            Playlist = [MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient1);

        try
        {
            // Only the FIRST of the TV's two connections drops.
            MusicHub hub = CreateHub(connectionId: tvConnectionId1, userId: userId);

            await hub.OnDisconnectedAsync(exception: null);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(expected: tvDeviceId);
            after.PlayState.Should().BeTrue();
            after.CurrentItem.Should().Be(expected: currentTrack);

            registry.TryGet(userId: userId, device: out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(expected: tvDeviceId);
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: tvConnectionId1, value: out _);
            connectedClients.Clients.TryRemove(key: tvConnectionId2, value: out _);
            stateManager.RemoveState(userId: userId);
            registry.Remove(userId: userId);
            UserCache.Current.RemoveUser(user: user);
        }
    }

    // Minimal IHttpContextAccessor stand-in — the real implementation is an
    // AsyncLocal-backed singleton unsuited to constructing an isolated
    // HttpContext per test.
    private sealed class HttpContextAccessorStub(HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
