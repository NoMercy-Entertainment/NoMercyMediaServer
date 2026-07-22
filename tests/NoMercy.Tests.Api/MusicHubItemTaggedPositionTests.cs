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
/// Covers the item-tagged twins of ReportPositionCommand/CurrentTimeCommand:
/// ReportPositionForItemCommand must accept a report tagged with the CURRENT
/// item, drop one tagged with any other item (no mutation, no broadcast), and
/// keep behaving like the untagged command when no tag is supplied at all —
/// the same real-DI MusicHub harness MusicHubActiveDeviceDisconnectTests uses.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class MusicHubItemTaggedPositionTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public MusicHubItemTaggedPositionTests(NoMercyApiFactory factory)
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
            Name = "Item Tag Test User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };
        UserCache.Current.AddUser(user: user);
        return user;
    }

    private (
        Guid UserId,
        User User,
        string ActiveConnectionId,
        string ActiveDeviceId,
        PlaylistTrackDto CurrentTrack,
        MusicPlayerState State
    ) SeedActiveSession()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string activeConnectionId = Guid.NewGuid().ToString();
        string activeDeviceId = $"web-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        Client activeClient = MakeClient(userId: userId, deviceId: activeDeviceId);
        connectedClients.Clients[key: activeConnectionId] = activeClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        PlaylistTrackDto currentTrack = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = activeDeviceId,
            PlayState = true,
            CurrentItem = currentTrack,
            Playlist = [MakeTrack()],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
            Time = 10_000,
            LastActiveHeartbeatUtc = DateTime.UtcNow,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: activeClient);

        return (userId, user, activeConnectionId, activeDeviceId, currentTrack, state);
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
    public async Task ReportPositionForItemCommand_MatchingItemId_AcceptsAndUpdatesPosition()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto currentTrack,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);
            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await hub.ReportPositionForItemCommand(positionMs: 5_000, itemId: currentTrack.Id.ToString());

            state.Time.Should().Be(expected: 5_000);
            state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(expected: before);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task ReportPositionForItemCommand_MismatchedItemId_DropsReport()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto _,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);
            long originalPositionCapturedAtMs = state.PositionCapturedAtMs;

            await hub.ReportPositionForItemCommand(positionMs: 5_000, itemId: Guid.NewGuid().ToString());

            // Untouched — the stale-tagged report never reaches the mutation.
            state.Time.Should().Be(expected: 10_000);
            state.PositionCapturedAtMs.Should().Be(expected: originalPositionCapturedAtMs);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task ReportPositionForItemCommand_NullItemId_BehavesLikeUntaggedReport()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto _,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);

            await hub.ReportPositionForItemCommand(positionMs: 7_500, itemId: null);

            state.Time.Should().Be(expected: 7_500);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task ReportPositionCommand_Untagged_StillWorks_Unchanged()
    {
        // Regression: the pre-existing untagged command must keep working
        // exactly as before — it has no itemId parameter to be stale-checked.
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto _,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);

            await hub.ReportPositionCommand(positionMs: 6_000);

            state.Time.Should().Be(expected: 6_000);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task CurrentTimeForItemCommand_ConvertsSecondsToMilliseconds()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto currentTrack,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);

            await hub.CurrentTimeForItemCommand(seconds: 3.5, itemId: currentTrack.Id.ToString());

            state.Time.Should().Be(expected: 3_500);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    // ── Liveness-first reordering (live-incident regression) ────────────────
    // TryRefreshHeartbeat must run BEFORE the ignore-window and stale-item drop
    // gates so a report from the ACTIVE device still proves it's alive even when
    // its position value gets rejected. A passive device's report must stay a
    // complete no-op for both liveness and position.

    [Fact]
    public async Task ReportPositionForItemCommand_InsideIgnoreWindow_RefreshesHeartbeat_ButDoesNotMutatePosition()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto currentTrack,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(value: 5);
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddSeconds(value: -10);

            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);

            await hub.ReportPositionForItemCommand(positionMs: 5_000, itemId: currentTrack.Id.ToString());

            // Dropped by the ignore window — position must be untouched...
            state.Time.Should().Be(expected: 10_000);
            // ...but the active device still proved it's alive right now.
            state
                .LastActiveHeartbeatUtc.Should()
                .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 2));
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task ReportPositionForItemCommand_MismatchedItemId_RefreshesHeartbeat_ButDoesNotMutatePosition()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto _,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddSeconds(value: -10);

            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);

            await hub.ReportPositionForItemCommand(positionMs: 5_000, itemId: Guid.NewGuid().ToString());

            // Dropped as stale-item — position untouched...
            state.Time.Should().Be(expected: 10_000);
            // ...but the active device still proved it's alive right now: a report
            // that arrives just after a track change is still proof the connection
            // is up, even though its position value can't be trusted.
            state
                .LastActiveHeartbeatUtc.Should()
                .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 2));
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task ReportPositionForItemCommand_FromPassiveDevice_RefreshesNothing()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto currentTrack,
            MusicPlayerState state
        ) = SeedActiveSession();

        string passiveConnectionId = Guid.NewGuid().ToString();
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        connectedClients.Clients[key: passiveConnectionId] = MakeClient(
            userId: userId,
            deviceId: $"passive-{Guid.NewGuid()}"
        );

        DateTime originalHeartbeat = DateTime.UtcNow.AddSeconds(value: -10);
        state.LastActiveHeartbeatUtc = originalHeartbeat;

        try
        {
            MusicHub hub = CreateHub(connectionId: passiveConnectionId, userId: userId);

            await hub.ReportPositionForItemCommand(positionMs: 5_000, itemId: currentTrack.Id.ToString());

            // A complete no-op: neither the position nor the liveness clock moves —
            // a passive mirror must never mask a truly-dead active device.
            state.Time.Should().Be(expected: 10_000);
            state.LastActiveHeartbeatUtc.Should().Be(expected: originalHeartbeat);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: [activeConnectionId, passiveConnectionId]);
        }
    }

    [Fact]
    public async Task ReportPositionCommand_InsideIgnoreWindow_RefreshesHeartbeat_ButDoesNotMutatePosition()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto _,
            MusicPlayerState state
        ) = SeedActiveSession();

        try
        {
            state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(value: 5);
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddSeconds(value: -10);

            MusicHub hub = CreateHub(connectionId: activeConnectionId, userId: userId);

            await hub.ReportPositionCommand(positionMs: 5_000);

            state.Time.Should().Be(expected: 10_000);
            state
                .LastActiveHeartbeatUtc.Should()
                .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 2));
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: activeConnectionId);
        }
    }

    [Fact]
    public async Task ReportPositionCommand_FromPassiveDevice_RefreshesNothing()
    {
        (
            Guid userId,
            User user,
            string activeConnectionId,
            string _,
            PlaylistTrackDto _,
            MusicPlayerState state
        ) = SeedActiveSession();

        string passiveConnectionId = Guid.NewGuid().ToString();
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        connectedClients.Clients[key: passiveConnectionId] = MakeClient(
            userId: userId,
            deviceId: $"passive-{Guid.NewGuid()}"
        );

        DateTime originalHeartbeat = DateTime.UtcNow.AddSeconds(value: -10);
        state.LastActiveHeartbeatUtc = originalHeartbeat;

        try
        {
            MusicHub hub = CreateHub(connectionId: passiveConnectionId, userId: userId);

            await hub.ReportPositionCommand(positionMs: 5_000);

            state.Time.Should().Be(expected: 10_000);
            state.LastActiveHeartbeatUtc.Should().Be(expected: originalHeartbeat);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: [activeConnectionId, passiveConnectionId]);
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
