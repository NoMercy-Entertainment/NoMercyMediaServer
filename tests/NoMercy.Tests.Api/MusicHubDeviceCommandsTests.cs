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
[Trait("Category", "Characterization")]
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
        return new(track, "US");
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
            .Setup(p =>
                p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

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

        MusicDeviceManager musicDeviceManager = new(new());
        MusicPlaylistManager musicPlaylistManager = new(new MusicRepository(contextFactory), new());
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
            Name = "Device Commands Test User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };
        UserCache.Current.AddUser(user);
        return user;
    }

    private void Cleanup(Guid userId, User user, params string[] connectionIds)
    {
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        foreach (string connectionId in connectionIds)
            connectedClients.Clients.TryRemove(connectionId, out _);

        _factory.Services.GetRequiredService<MusicPlayerStateManager>().RemoveState(userId);
        _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>().Remove(userId);
        UserCache.Current.RemoveUser(user);
    }

    // =========================================================================
    // ChangeDeviceCommand — non-TV transfer (no Cast launch involved)
    // =========================================================================

    [Fact]
    public async Task ChangeDeviceCommand_NonTvTarget_TransfersDeviceIdAndResolvesVolume()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);

        string phoneConnectionId = Guid.NewGuid().ToString();
        string tabletConnectionId = Guid.NewGuid().ToString();
        string phoneDeviceId = $"phone-{Guid.NewGuid()}";
        string tabletDeviceId = $"tablet-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client phoneClient, _) = MakeClientWithProxy(userId, phoneDeviceId, "web");
        (Client tabletClient, _) = MakeClientWithProxy(
            userId,
            tabletDeviceId,
            "web",
            volumePercent: 65
        );
        connectedClients.Clients[phoneConnectionId] = phoneClient;
        connectedClients.Clients[tabletConnectionId] = tabletClient;

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
            CurrentList = new("/music/albums/test", UriKind.Relative),
            Time = 15_000,
        };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, phoneClient);

        try
        {
            MusicHub hub = CreateHub(phoneConnectionId, userId);

            await hub.ChangeDeviceCommand(tabletDeviceId);

            stateManager.TryGetValue(userId, out MusicPlayerState? after).Should().BeTrue();
            after!.DeviceId.Should().Be(tabletDeviceId);
            // No prior DeviceVolumes entry for the tablet, so it falls back to
            // the target device's own persisted VolumePercent (65).
            after.VolumePercentage.Should().Be(65);
            after.DeviceVolumes[tabletDeviceId].Should().Be(65);

            registry.TryGet(userId, out Device? active).Should().BeTrue();
            active!.DeviceId.Should().Be(tabletDeviceId);
        }
        finally
        {
            Cleanup(userId, user, phoneConnectionId, tabletConnectionId);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_NoExistingPlayerState_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);
        string connectionId = Guid.NewGuid().ToString();

        try
        {
            MusicHub hub = CreateHub(connectionId, userId);

            Func<Task> act = async () => await hub.ChangeDeviceCommand("some-device-id");

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(userId, user, connectionId);
        }
    }

    [Fact]
    public async Task ChangeDeviceCommand_UnknownCachedUser_IsNoOp()
    {
        // Deliberately never seeded into UserCache.
        Guid unknownUserId = Guid.NewGuid();
        string connectionId = Guid.NewGuid().ToString();

        MusicHub hub = CreateHub(connectionId, unknownUserId);

        Func<Task> act = async () => await hub.ChangeDeviceCommand("some-device-id");

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // SetDeviceVolumeCommand / ChangeVolumeCommand
    // =========================================================================

    [Fact]
    public async Task SetDeviceVolumeCommand_ActiveDeviceTarget_UpdatesScopedVolumePercentage()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);
        string connectionId = Guid.NewGuid().ToString();
        string deviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client client, _) = MakeClientWithProxy(userId, deviceId, "tv");
        connectedClients.Clients[connectionId] = client;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        MusicPlayerState state = new() { DeviceId = deviceId, PlayState = true };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, client);

        try
        {
            MusicHub hub = CreateHub(connectionId, userId);

            await hub.SetDeviceVolumeCommand(deviceId, 77);

            state.VolumePercentage.Should().Be(77);
            state.DeviceVolumes[deviceId].Should().Be(77);
            client.VolumePercent.Should().Be(77);
        }
        finally
        {
            Cleanup(userId, user, connectionId);
        }
    }

    [Fact]
    public async Task SetDeviceVolumeCommand_NonActiveDeviceTarget_UpdatesDeviceVolumesOnly()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);
        string activeConnectionId = Guid.NewGuid().ToString();
        string passiveConnectionId = Guid.NewGuid().ToString();
        string activeDeviceId = $"tv-{Guid.NewGuid()}";
        string passiveDeviceId = $"phone-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client activeClient, _) = MakeClientWithProxy(userId, activeDeviceId, "tv");
        (Client passiveClient, _) = MakeClientWithProxy(userId, passiveDeviceId, "web");
        connectedClients.Clients[activeConnectionId] = activeClient;
        connectedClients.Clients[passiveConnectionId] = passiveClient;

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
        stateManager.UpdateState(userId, state);
        registry.Set(userId, activeClient);

        try
        {
            // The PASSIVE phone adjusts ITS OWN slider — must not move the
            // active TV's scoped VolumePercentage.
            MusicHub hub = CreateHub(passiveConnectionId, userId);

            await hub.SetDeviceVolumeCommand(passiveDeviceId, 20);

            state.VolumePercentage.Should().Be(50);
            state.DeviceVolumes[passiveDeviceId].Should().Be(20);
            passiveClient.VolumePercent.Should().Be(20);
        }
        finally
        {
            Cleanup(userId, user, activeConnectionId, passiveConnectionId);
        }
    }

    [Fact]
    public async Task SetDeviceVolumeCommand_NullVolume_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);
        string connectionId = Guid.NewGuid().ToString();

        try
        {
            MusicHub hub = CreateHub(connectionId, userId);

            Func<Task> act = async () => await hub.SetDeviceVolumeCommand("any-device", null);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(userId, user, connectionId);
        }
    }

    [Fact]
    public async Task SetDeviceVolumeCommand_UnknownDeviceIdAndNoActiveDevice_IsNoOp()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);
        string connectionId = Guid.NewGuid().ToString();

        try
        {
            MusicHub hub = CreateHub(connectionId, userId);

            Func<Task> act = async () =>
                await hub.SetDeviceVolumeCommand("device-that-does-not-exist", 50);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(userId, user, connectionId);
        }
    }

    [Fact]
    public async Task ChangeVolumeCommand_NullDeviceId_TargetsCurrentActiveDevice()
    {
        Guid userId = Guid.NewGuid();
        User user = SeedTestUser(userId);
        string connectionId = Guid.NewGuid().ToString();
        string activeDeviceId = $"tv-{Guid.NewGuid()}";

        ConnectedClients connectedClients = _factory.GetConnectedClients();
        (Client activeClient, _) = MakeClientWithProxy(userId, activeDeviceId, "tv");
        connectedClients.Clients[connectionId] = activeClient;

        MusicPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<MusicPlayerStateManager>();
        MusicActiveDeviceRegistry registry =
            _factory.Services.GetRequiredService<MusicActiveDeviceRegistry>();

        MusicPlayerState state = new() { DeviceId = activeDeviceId, PlayState = true };
        stateManager.UpdateState(userId, state);
        registry.Set(userId, activeClient);

        try
        {
            MusicHub hub = CreateHub(connectionId, userId);

            await hub.ChangeVolumeCommand(33);

            state.VolumePercentage.Should().Be(33);
            activeClient.VolumePercent.Should().Be(33);
        }
        finally
        {
            Cleanup(userId, user, connectionId);
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
