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

// Regression coverage for the live incident: a music session sat PAUSED with its
// active device set to a TV whose app process was killed. The liveness sweep
// (MusicPlaybackService.IsActiveDeviceStale) only acts while PlayState is
// playing, so a wedged paused session survived forever and every command from
// other devices relayed into a void. The fix is authority-level: MusicHub's
// OnDisconnectedAsync now releases (or ends) the session the moment the
// connection belonging to the CURRENT active device actually disconnects,
// regardless of play state — graceful release while playing, clean
// item:null end while paused/idle. These tests build a real MusicHub against
// the app's actual DI-configured singletons (MusicPlayerStateManager,
// MusicActiveDeviceRegistry, ConnectedClients) via NoMercyApiFactory, mocking
// only the SignalR plumbing a live connection would normally supply.
[Trait("Category", "Characterization")]
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
        return new(track, "US");
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

        MusicDeviceManager musicDeviceManager = new(new MediaContext());
        MusicPlaylistManager musicPlaylistManager = new(
            new MusicRepository(contextFactory),
            new MediaContext()
        );
        DeviceBusRegistry busRegistry = new(contextFactory, Mock.Of<IHubContext<DeviceHub>>());
        CastSessionTokenService castTokenService = new(authManager, new AuthTokenStore());

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/musicHub";

        MusicHub hub = new(
            NullLogger<MusicHub>.Instance,
            new HttpContextAccessorStub(httpContext),
            contextFactory,
            connectedClients,
            clientMessenger,
            musicPlaybackService,
            stateManager,
            musicDeviceManager,
            musicPlaylistManager,
            commandHandler,
            Mock.Of<IActivityLogger>(),
            busRegistry,
            castTokenService,
            Mock.Of<IChromeCastService>(),
            castPanelWakeLauncher,
            activeDeviceRegistry
        );

        ClaimsPrincipal principal = new(
            new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth")
        );

        Mock<HubCallerContext> context = new();
        context.Setup(c => c.User).Returns(principal);
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        Mock<ISingleClientProxy> callerProxy = new();
        Mock<ISingleClientProxy> userProxy = new();

        Mock<IHubCallerClients> clients = new();
        clients.Setup(c => c.Caller).Returns(callerProxy.Object);
        clients.Setup(c => c.User(It.IsAny<string>())).Returns(userProxy.Object);

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
        UserCache.Current.AddUser(user);
        return user;
    }

    [Fact]
    public async Task LastConnectionOfActiveDevice_Disconnects_WhilePaused_EndsSessionCleanly()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client phoneClient = MakeClient(userId, phoneDeviceId);
        Client tvClient = MakeClient(userId, tvDeviceId, "tv");
        connectedClients.Clients[phoneConnectionId] = phoneClient;
        connectedClients.Clients[tvConnectionId] = tvClient;

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
            CurrentList = new("/music/albums/test", UriKind.Relative),
        };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, tvClient);

        try
        {
            MusicHub hub = CreateHub(tvConnectionId, userId);

            await hub.OnDisconnectedAsync(null);

            stateManager.TryGetValue(userId, out MusicPlayerState? after).Should().BeTrue();
            after!.CurrentItem.Should().BeNull();
            after.PlayState.Should().BeFalse();
            after.DeviceId.Should().BeNull();
            after.Playlist.Should().BeEmpty();
            after.Backlog.Should().BeEmpty();
            after.Actions.Disallows.Resuming.Should().BeTrue();
            after.Actions.Disallows.Pausing.Should().BeTrue();
            after.Actions.Disallows.Stopping.Should().BeTrue();

            // The dead TV must no longer be recorded as active — the very next
            // claim from any device (including the still-connected phone) must
            // be free to become active.
            registry.TryGet(userId, out Device? active).Should().BeFalse();
        }
        finally
        {
            connectedClients.Clients.TryRemove(phoneConnectionId, out _);
            connectedClients.Clients.TryRemove(tvConnectionId, out _);
            stateManager.RemoveState(userId);
            registry.Remove(userId);
            UserCache.Current.RemoveUser(user);
        }
    }

    [Fact]
    public async Task LastConnectionOfActiveDevice_Disconnects_WhilePlaying_ReleasesActive_ButSessionSurvives()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client phoneClient = MakeClient(userId, phoneDeviceId);
        Client tvClient = MakeClient(userId, tvDeviceId, "tv");
        connectedClients.Clients[phoneConnectionId] = phoneClient;
        connectedClients.Clients[tvConnectionId] = tvClient;

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
            CurrentList = new("/music/albums/test", UriKind.Relative),
        };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, tvClient);

        try
        {
            MusicHub hub = CreateHub(tvConnectionId, userId);

            await hub.OnDisconnectedAsync(null);

            stateManager.TryGetValue(userId, out MusicPlayerState? after).Should().BeTrue();
            // Graceful release: the session survives...
            after!.CurrentItem.Should().Be(currentTrack);
            after.Playlist.Should().HaveCount(2);
            // ...but nobody owns it anymore, and PlayState reflects that nothing
            // is actually producing audio right now.
            after.PlayState.Should().BeFalse();
            after.DeviceId.Should().BeNull();
            after.Actions.Disallows.Resuming.Should().BeFalse();

            registry.TryGet(userId, out Device? active).Should().BeFalse();
        }
        finally
        {
            connectedClients.Clients.TryRemove(phoneConnectionId, out _);
            connectedClients.Clients.TryRemove(tvConnectionId, out _);
            stateManager.RemoveState(userId);
            registry.Remove(userId);
            UserCache.Current.RemoveUser(user);
        }
    }

    [Fact]
    public async Task NonActiveDeviceDisconnects_LeavesActiveSessionUntouched()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client phoneClient = MakeClient(userId, phoneDeviceId);
        Client tvClient = MakeClient(userId, tvDeviceId, "tv");
        connectedClients.Clients[phoneConnectionId] = phoneClient;
        connectedClients.Clients[tvConnectionId] = tvClient;

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
            CurrentList = new("/music/albums/test", UriKind.Relative),
        };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, tvClient);

        try
        {
            // The PASSIVE phone disconnects, not the active TV.
            MusicHub hub = CreateHub(phoneConnectionId, userId);

            await hub.OnDisconnectedAsync(null);

            stateManager.TryGetValue(userId, out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(tvDeviceId);
            after.PlayState.Should().BeTrue();
            after.CurrentItem.Should().Be(currentTrack);

            registry.TryGet(userId, out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(tvDeviceId);
        }
        finally
        {
            connectedClients.Clients.TryRemove(phoneConnectionId, out _);
            connectedClients.Clients.TryRemove(tvConnectionId, out _);
            stateManager.RemoveState(userId);
            registry.Remove(userId);
            UserCache.Current.RemoveUser(user);
        }
    }

    [Fact]
    public async Task OneOfTwoConnectionsForSameActiveDevice_Drops_LeavesSessionUntouched()
    {
        // Simulates the KMP double-connect: the same device_id briefly holds two
        // live hub connections. Tearing down one must never release the device
        // while its other connection is still live.
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);

        string tvConnectionId1 = Guid.NewGuid().ToString();
        string tvConnectionId2 = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client tvClient1 = MakeClient(userId, tvDeviceId, "tv");
        Client tvClient2 = MakeClient(userId, tvDeviceId, "tv");
        connectedClients.Clients[tvConnectionId1] = tvClient1;
        connectedClients.Clients[tvConnectionId2] = tvClient2;

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
            CurrentList = new("/music/albums/test", UriKind.Relative),
        };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, tvClient1);

        try
        {
            // Only the FIRST of the TV's two connections drops.
            MusicHub hub = CreateHub(tvConnectionId1, userId);

            await hub.OnDisconnectedAsync(null);

            stateManager.TryGetValue(userId, out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(tvDeviceId);
            after.PlayState.Should().BeTrue();
            after.CurrentItem.Should().Be(currentTrack);

            registry.TryGet(userId, out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(tvDeviceId);
        }
        finally
        {
            connectedClients.Clients.TryRemove(tvConnectionId1, out _);
            connectedClients.Clients.TryRemove(tvConnectionId2, out _);
            stateManager.RemoveState(userId);
            registry.Remove(userId);
            UserCache.Current.RemoveUser(user);
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
