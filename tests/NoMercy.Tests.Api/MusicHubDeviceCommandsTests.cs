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
/// Requirement-driven coverage for MusicHub.Devices.cs' ChangeDeviceCommand (the
/// non-TV / no-cast-launch transfer path) and SetDeviceVolumeCommand /
/// ChangeVolumeCommand — the hub methods themselves were previously untested;
/// only the pure MusicVolumeResolver.Clamp helper had coverage (MusicHubVolumeTests).
/// Builds a real MusicHub against the app's DI-configured MediaContext (via
/// NoMercyApiFactory), mocking only the SignalR plumbing and IChromeCastService
/// (native Cast SDK has no test double).
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class MusicHubDeviceCommandsTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public MusicHubDeviceCommandsTests(NoMercyApiFactory factory)
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
        string type = "web",
        int? volumePercent = null
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
            VolumePercent = volumePercent,
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
            Name = "Device Commands Test User",
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

    // =========================================================================
    // ChangeDeviceCommand — non-TV transfer (no Cast launch involved)
    // =========================================================================

    [Fact]
    public async Task ChangeDeviceCommand_NonTvTarget_TransfersDeviceIdAndResolvesVolume()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tabletConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tabletDeviceId = $"tablet-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client phoneClient, _) = MakeClientWithProxy(userId: userId, deviceId: phoneDeviceId, type: "web");
        (Client tabletClient, _) = MakeClientWithProxy(
            userId: userId,
            deviceId: tabletDeviceId,
            type: "web",
            volumePercent: 65
        );
        connectedClients.Clients[key: phoneConnectionId] = phoneClient;
        connectedClients.Clients[key: tabletConnectionId] = tabletClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        PlaylistTrackDto currentTrack = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = phoneDeviceId,
            PlayState = true,
            CurrentItem = currentTrack,
            Playlist = [currentTrack],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
            Time = 15_000,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: phoneClient);

        try
        {
            MusicHub hub = CreateHub(connectionId: phoneConnectionId, userId: userId);

            await hub.ChangeDeviceCommand(deviceId: tabletDeviceId);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(expected: tabletDeviceId);
            // No prior DeviceVolumes entry for the tablet, so it falls back to
            // the target device's own persisted VolumePercent (65).
            after.VolumePercentage.Should().Be(expected: 65);
            after.DeviceVolumes[key: tabletDeviceId].Should().Be(expected: 65);

            registry.TryGet(userId: userId, device: out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(expected: tabletDeviceId);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: [phoneConnectionId, tabletConnectionId]);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_NoExistingPlayerState_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);
        string connectionId = Guid.NewGuid().ToString();

        try
        {
            MusicHub hub = CreateHub(connectionId: connectionId, userId: userId);

            Func<Task> act = async () => await hub.ChangeDeviceCommand(deviceId: "some-device-id");

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: connectionId);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_UnknownCachedUser_IsNoOp()
    {
        // Deliberately never seeded into UserCache.
        Guid unknownUserId = Guid.NewGuid();
        string connectionId = Guid.NewGuid().ToString();

        MusicHub hub = CreateHub(connectionId: connectionId, userId: unknownUserId);

        Func<Task> act = async () => await hub.ChangeDeviceCommand(deviceId: "some-device-id");

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // SetDeviceVolumeCommand / ChangeVolumeCommand
    // =========================================================================

    [Fact]
    public async Task SetDeviceVolumeCommand_ActiveDeviceTarget_UpdatesScopedVolumePercentage()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);
        string connectionId = Guid.NewGuid().ToString();
        string deviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client client, _) = MakeClientWithProxy(userId: userId, deviceId: deviceId, type: "tv");
        connectedClients.Clients[key: connectionId] = client;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        MusicPlayerState state = new() { DeviceId = deviceId, PlayState = true };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: client);

        try
        {
            MusicHub hub = CreateHub(connectionId: connectionId, userId: userId);

            await hub.SetDeviceVolumeCommand(deviceId: deviceId, volume: 77);

            state.VolumePercentage.Should().Be(expected: 77);
            state.DeviceVolumes[key: deviceId].Should().Be(expected: 77);
            client.VolumePercent.Should().Be(expected: 77);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: connectionId);
        }
    }

    [Fact]
    public async Task SetDeviceVolumeCommand_NonActiveDeviceTarget_UpdatesDeviceVolumesOnly()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);
        string activeConnectionId = Guid.NewGuid().ToString();
        string passiveConnectionId = Guid.NewGuid().ToString();
        string activeDeviceId = $"tv-{Guid.NewGuid()}";
        string passiveDeviceId = $"phone-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client activeClient, _) = MakeClientWithProxy(userId: userId, deviceId: activeDeviceId, type: "tv");
        (Client passiveClient, _) = MakeClientWithProxy(userId: userId, deviceId: passiveDeviceId, type: "web");
        connectedClients.Clients[key: activeConnectionId] = activeClient;
        connectedClients.Clients[key: passiveConnectionId] = passiveClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        MusicPlayerState state = new()
        {
            DeviceId = activeDeviceId,
            PlayState = true,
            VolumePercentage = 50,
        };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: activeClient);

        try
        {
            // The PASSIVE phone adjusts ITS OWN slider — must not move the
            // active TV's scoped VolumePercentage.
            MusicHub hub = CreateHub(connectionId: passiveConnectionId, userId: userId);

            await hub.SetDeviceVolumeCommand(deviceId: passiveDeviceId, volume: 20);

            state.VolumePercentage.Should().Be(expected: 50);
            state.DeviceVolumes[key: passiveDeviceId].Should().Be(expected: 20);
            passiveClient.VolumePercent.Should().Be(expected: 20);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: [activeConnectionId, passiveConnectionId]);
        }
    }

    [Fact]
    public async Task SetDeviceVolumeCommand_NullVolume_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);
        string connectionId = Guid.NewGuid().ToString();

        try
        {
            MusicHub hub = CreateHub(connectionId: connectionId, userId: userId);

            Func<Task> act = async () => await hub.SetDeviceVolumeCommand(deviceId: "any-device", volume: null);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: connectionId);
        }
    }

    [Fact]
    public async Task SetDeviceVolumeCommand_UnknownDeviceIdAndNoActiveDevice_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);
        string connectionId = Guid.NewGuid().ToString();

        try
        {
            MusicHub hub = CreateHub(connectionId: connectionId, userId: userId);

            Func<Task> act = async () =>
                await hub.SetDeviceVolumeCommand(deviceId: "device-that-does-not-exist", volume: 50);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: connectionId);
        }
    }

    [Fact]
    public async Task ChangeVolumeCommand_NullDeviceId_TargetsCurrentActiveDevice()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId: userId);
        string connectionId = Guid.NewGuid().ToString();
        string activeDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client activeClient, _) = MakeClientWithProxy(userId: userId, deviceId: activeDeviceId, type: "tv");
        connectedClients.Clients[key: connectionId] = activeClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        MusicPlayerState state = new() { DeviceId = activeDeviceId, PlayState = true };
        stateManager.UpdateState(userId: userId, state: state);
        registry.Set(userId: userId, device: activeClient);

        try
        {
            MusicHub hub = CreateHub(connectionId: connectionId, userId: userId);

            await hub.ChangeVolumeCommand(volume: 33);

            state.VolumePercentage.Should().Be(expected: 33);
            activeClient.VolumePercent.Should().Be(expected: 33);
        }
        finally
        {
            Cleanup(userId: userId, user: user, connectionIds: connectionId);
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
