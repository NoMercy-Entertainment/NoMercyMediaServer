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

/// <summary>
/// Covers the empty/null <see cref="MusicHub.ChangeDeviceCommand"/> release contract: the KMP
/// standby job has always sent ChangeDeviceCommand("") intending "release my active claim",
/// but the server silently ignored it — the client-side half of the contract existed with no
/// server-side implementation. From the ACTIVE device this must release the claim (clear
/// DeviceId, drop the active-device registry entry, stop the timer, pause) while keeping
/// CurrentItem/Playlist/Backlog/position untouched — the device is going dark, not ending the
/// session. From a PASSIVE device it must stay a no-op, exactly like before this existed, so it
/// can never release someone else's claim by accident.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class MusicHubDeviceReleaseTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public MusicHubDeviceReleaseTests(NoMercyApiFactory factory)
    {
        _factory = factory;
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

    private static (Client Client, Mock<ISingleClientProxy> Proxy) MakeClientWithProxy(
        Guid userId,
        string deviceId,
        string type = "web"
    )
    {
        Mock<ISingleClientProxy> proxy = new();
        proxy
            .Setup(expression: p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(value: Task.CompletedTask);

        Client client = new()
        {
            Id = Ulid.NewUlid(),
            Sub = userId,
            DeviceId = deviceId,
            Endpoint = "/musicHub",
            Type = type,
            Socket = proxy.Object,
        };
        return (client, proxy);
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
        MusicPlaylistManager musicPlaylistManager = new(
            musicService: new MusicRepository(contextFactory: contextFactory),
            mediaContext: new()
        );
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
            Name = "Device Release Test User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };
        UserCache.Current.AddUser(user: user);
        return user;
    }

    private void Cleanup(Guid userId, User user, params string[] connectionIds)
    {
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        foreach (string connectionId in connectionIds)
            connectedClients.Clients.TryRemove(key: connectionId, value: out _);

        _factory.Services.GetRequiredService<MusicPlayerStateManager>().RemoveState(userId: userId);
        _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>().Remove(userId: userId);
        UserCache.Current.RemoveUser(user: user);
    }

    [Fact]
    public async Task ChangeDeviceCommand_EmptyDeviceId_FromActiveDevice_ReleasesClaim_KeepsItemAndPosition()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string tvConnectionId = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client tvClient, Mock<ISingleClientProxy> tvProxy) = MakeClientWithProxy(
            userId: userId,
            deviceId: tvDeviceId,
            type: "tv"
        );
        connectedClients.Clients[key: tvConnectionId] = tvClient;

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
            Backlog = [MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
            Time = 42_000,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            MusicHub hub = CreateHub(connectionId: tvConnectionId, userId: userId);

            await hub.ChangeDeviceCommand(deviceId: "");

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().BeNull();
            after.PlayState.Should().BeFalse();

            // Device going dark, not the session ending: item/queue/position all survive.
            after.CurrentItem.Should().Be(expected: currentTrack);
            after.Playlist.Should().HaveCount(expected: 1);
            after.Backlog.Should().HaveCount(expected: 1);
            after.Time.Should().Be(expected: 42_000);

            // A resume from any device must be allowed next.
            after.Actions.Disallows.Resuming.Should().BeFalse();

            registry.TryGet(userId: userId, device: out _).Should().BeFalse();

            tvProxy.Verify(
                expression: p =>
                    p.SendCoreAsync(
                        "MusicPlayerState",
                        It.IsAny<object?[]>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.AtLeastOnce
            );
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: tvConnectionId);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_NullDeviceId_BehavesIdenticallyToEmpty()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string tvConnectionId = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client tvClient, _) = MakeClientWithProxy(userId: userId, deviceId: tvDeviceId, type: "tv");
        connectedClients.Clients[key: tvConnectionId] = tvClient;

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
            Time = 5_000,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            MusicHub hub = CreateHub(connectionId: tvConnectionId, userId: userId);

            await hub.ChangeDeviceCommand(deviceId: null);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().BeNull();
            after.PlayState.Should().BeFalse();
            after.CurrentItem.Should().Be(expected: currentTrack);
            after.Time.Should().Be(expected: 5_000);

            registry.TryGet(userId: userId, device: out _).Should().BeFalse();
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: tvConnectionId);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_EmptyDeviceId_FromPassiveDevice_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client tvClient, _) = MakeClientWithProxy(userId: userId, deviceId: tvDeviceId, type: "tv");
        (Client phoneClient, _) = MakeClientWithProxy(userId: userId, deviceId: phoneDeviceId);
        connectedClients.Clients[key: tvConnectionId] = tvClient;
        connectedClients.Clients[key: phoneConnectionId] = phoneClient;

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
            Time = 10_000,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            // The PASSIVE phone sends the release — must never end the TV's claim.
            MusicHub hub = CreateHub(connectionId: phoneConnectionId, userId: userId);

            await hub.ChangeDeviceCommand(deviceId: "");

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(expected: tvDeviceId);
            after.PlayState.Should().BeTrue();
            after.CurrentItem.Should().Be(expected: currentTrack);
            after.Time.Should().Be(expected: 10_000);

            registry.TryGet(userId: userId, device: out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(expected: tvDeviceId);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: [tvConnectionId, phoneConnectionId]);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_AfterRelease_CommandFromAnotherDevice_ClaimsActiveImmediately()
    {
        // Mirrors the real KMP flow: TV standby releases via ChangeDeviceCommand(""), then
        // the phone issues an ordinary command (PlaybackCommand) with no explicit device
        // switch — it must claim active immediately because nobody owns the session
        // anymore, exactly like StartPlaybackCommand's own GetOrPromoteActiveDevice would
        // for a brand-new session.
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string tvConnectionId = Guid.NewGuid().ToString();
        string phoneConnectionId = Guid.NewGuid().ToString();
        string tvDeviceId = $"tv-{Guid.NewGuid()}";
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client tvClient, _) = MakeClientWithProxy(userId: userId, deviceId: tvDeviceId, type: "tv");
        (Client phoneClient, _) = MakeClientWithProxy(userId: userId, deviceId: phoneDeviceId);
        connectedClients.Clients[key: tvConnectionId] = tvClient;
        connectedClients.Clients[key: phoneConnectionId] = phoneClient;

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
            Time = 10_000,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: tvClient);

        try
        {
            MusicHub tvHub = CreateHub(connectionId: tvConnectionId, userId: userId);
            await tvHub.ChangeDeviceCommand(deviceId: "");

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? afterRelease).Should().BeTrue();
            afterRelease!.DeviceId.Should().BeNull();

            MusicHub phoneHub = CreateHub(connectionId: phoneConnectionId, userId: userId);
            await phoneHub.PlaybackCommand(command: "play");

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? afterClaim).Should().BeTrue();
            afterClaim!.DeviceId.Should().Be(expected: phoneDeviceId);
            afterClaim.PlayState.Should().BeTrue();
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: [tvConnectionId, phoneConnectionId]);
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
