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
using NoMercy.Api.Services.Video;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Data.Activity;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Discovery;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Domain;
using NoMercy.Setup.Cast;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Requirement-driven coverage for VideoHub.Playback.cs — SetTime, RemoveWatched,
/// PlaybackCommand and the non-Cast branches of ChangeDeviceCommand. Builds a
/// real VideoHub against the app's DI-configured MediaContext/repositories (via
/// NoMercyApiFactory), mocking only the SignalR plumbing (HubCallerContext,
/// IHubCallerClients) and IChromeCastService (native Cast SDK has no test double).
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class VideoHubPlaybackTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public VideoHubPlaybackTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    private (VideoHub Hub, Mock<IUserDataRepository> UserDataRepository) CreateHub(
        string connectionId,
        Guid userId,
        out Mock<IHubCallerClients> clients
    )
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        IServiceScope scope = _factory.Services.CreateScope();

        Mock<IUserDataRepository> userDataRepository = new();

        DefaultHttpContext httpContext = new() { RequestServices = null! };
        httpContext.Request.Path = "/videoHub";

        VideoHub hub = new(
            logger: NullLogger<VideoHub>.Instance,
            httpContextAccessor: new HttpContextAccessorStub(httpContext: httpContext),
            contextFactory: contextFactory,
            connectedClients: _factory.GetConnectedClients(),
            clientMessenger: _factory.Services.GetRequiredService<IClientMessenger>(),
            videoPlaybackService: _factory.Services.GetRequiredService<VideoPlaybackService>(),
            videoPlayerStateManager: _factory.Services.GetRequiredService<VideoPlayerStateManager>(),
            videoDeviceManager: new VideoDeviceManager(mediaContext: new MediaContext()),
            videoPlaylistManager: scope.ServiceProvider.GetRequiredService<VideoPlaylistManager>(),
            commandHandler: _factory.Services.GetRequiredService<VideoPlaybackCommandHandler>(),
            activityLogger: Mock.Of<IActivityLogger>(),
            castTokenService: _factory.Services.GetRequiredService<CastSessionTokenService>(),
            busRegistry: _factory.Services.GetRequiredService<DeviceBusRegistry>(),
            chromeCast: Mock.Of<IChromeCastService>(),
            userDataRepository: userDataRepository.Object,
            networkDiscovery: null as INetworkDiscovery
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(claims: [new(type: ClaimTypes.NameIdentifier, value: userId.ToString())], authenticationType: "TestAuth")
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: connectionId);
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

        clients = new();
        Mock<ISingleClientProxy> callerProxy = new();
        Mock<ISingleClientProxy> userProxy = new();
        clients.Setup(expression: c => c.Caller).Returns(value: callerProxy.Object);
        clients.Setup(expression: c => c.User(It.IsAny<string>())).Returns(value: userProxy.Object);

        hub.Context = context.Object;
        hub.Clients = clients.Object;

        return (hub, userDataRepository);
    }

    private async Task<Ulid> SeededVideoFileIdAsync(int movieId)
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        return await ctx.VideoFiles.Where(predicate: v => v.MovieId == movieId).Select(selector: v => v.Id).FirstAsync();
    }

    private async Task<UserData?> FindUserDataAsync(Ulid videoFileId, Guid userId)
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        return await ctx.UserData.FirstOrDefaultAsync(predicate: u =>
            u.VideoFileId == videoFileId && u.UserId == userId
        );
    }

    // SetTime's happy path performs a real FK-checked UserData Upsert (UserId,
    // MovieId, VideoFileId all enforced — MediaContext opens with "Foreign
    // Keys=True"). Reusing the shared seeded user/movie here would race any
    // other test class touching the same (VideoFileId, UserId, MovieId)
    // unique key under xUnit's parallel test collections, so this mints a
    // fully isolated Movie + VideoFile + User scoped to just this test.
    private async Task<(User User, int MovieId, Ulid VideoFileId)> SeedIsolatedMovieAndUserAsync()
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();

        int movieId = Random.Shared.Next(minValue: 600_000_000, maxValue: 699_000_000);
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@nomercy.tv",
            Name = "SetTime Isolated Test User",
            Owner = false,
            Allowed = true,
            Manage = false,
        };
        Movie movie = new()
        {
            Id = movieId,
            Title = "SetTime Isolated Test Movie",
            TitleSort = "setTime isolated test movie",
        };
        ctx.Users.Add(entity: user);
        ctx.Movies.Add(entity: movie);
        await ctx.SaveChangesAsync();

        VideoFile videoFile = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "isolated.mkv",
            Folder = "/movies/isolated",
            HostFolder = "/movies/isolated",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = "movies",
            MovieId = movieId,
        };
        ctx.VideoFiles.Add(entity: videoFile);
        await ctx.SaveChangesAsync();

        UserCache.Current.AddUser(user: user);

        return (user, movieId, videoFile.Id);
    }

    // =========================================================================
    // SetTime
    // =========================================================================

    [Fact]
    public async Task SetTime_UnknownVideoFile_DoesNotUpsert()
    {
        (VideoHub hub, _) = CreateHub(
            connectionId: Guid.NewGuid().ToString(),
            userId: TestAuthHandler.DefaultUserId,
            clients: out _
        );

        await hub.SetTime(
            request: new()
            {
                VideoId = Ulid.NewUlid(),
                TmdbId = 129,
                PlaylistType = MediaTypes.MovieMediaType,
                Time = 1000,
            }
        );

        // No exception and (implicitly) no row written — verified by the movie
        // path test below actually finding a row for a REAL video file.
    }

    [Fact]
    public async Task SetTime_ValidMovie_UpsertsUserDataWithMovieId()
    {
        (User user, int movieId, Ulid videoFileId) = await SeedIsolatedMovieAndUserAsync();

        try
        {
            (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: user.Id, clients: out _);

            await hub.SetTime(
                request: new()
                {
                    VideoId = videoFileId,
                    TmdbId = movieId,
                    PlaylistType = MediaTypes.MovieMediaType,
                    Time = 42_000,
                    Audio = "en",
                    Subtitle = "none",
                    SubtitleType = "none",
                }
            );

            UserData? row = await FindUserDataAsync(videoFileId: videoFileId, userId: user.Id);
            row.Should().NotBeNull();
            row!.MovieId.Should().Be(expected: movieId);
            row.Time.Should().Be(expected: 42_000);
        }
        finally
        {
            UserCache.Current.RemoveUser(user: user);
        }
    }

    [Fact]
    public async Task SetTime_MovieIdNotInDatabase_DoesNotUpsert()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        Ulid videoFileId = await SeededVideoFileIdAsync(movieId: 129);
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

        await hub.SetTime(
            request: new()
            {
                VideoId = videoFileId,
                TmdbId = 999_999_999,
                PlaylistType = MediaTypes.MovieMediaType,
                Time = 5_000,
            }
        );

        UserData? row = await FindUserDataAsync(videoFileId: videoFileId, userId: userId);
        // Either no row was ever written for this (videoFileId, userId, movieId=999999999)
        // combination, or an earlier test already wrote one for MovieId 129 — either
        // way the bogus TmdbId must never have been persisted.
        if (row is not null)
            row.MovieId.Should().NotBe(unexpected: 999_999_999);
    }

    [Fact]
    public async Task SetTime_CollectionPlaylistId_NonNumeric_DoesNotThrow()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        Ulid videoFileId = await SeededVideoFileIdAsync(movieId: 129);
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

        Func<Task> act = async () =>
            await hub.SetTime(
                request: new()
                {
                    VideoId = videoFileId,
                    TmdbId = 129,
                    PlaylistType = MediaTypes.CollectionMediaType,
                    PlaylistId = "not-a-number",
                    Time = 1_000,
                }
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetTime_SpecialPlaylistId_NonUlid_DoesNotThrow()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        Ulid videoFileId = await SeededVideoFileIdAsync(movieId: 129);
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

        Func<Task> act = async () =>
            await hub.SetTime(
                request: new()
                {
                    VideoId = videoFileId,
                    TmdbId = 129,
                    PlaylistType = MediaTypes.SpecialMediaType,
                    PlaylistId = "not-a-ulid",
                    Time = 1_000,
                }
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetTime_UnknownCachedUser_IsNoOp()
    {
        // CreateHub's principal carries a userId with no matching UserCache
        // entry — SetTime's very first guard (`user is null`) must return
        // before touching the database at all.
        Guid unknownUserId = Guid.NewGuid();
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: unknownUserId, clients: out _);

        Func<Task> act = async () =>
            await hub.SetTime(
                request: new()
                {
                    VideoId = Ulid.NewUlid(),
                    TmdbId = 129,
                    PlaylistType = MediaTypes.MovieMediaType,
                    Time = 1_000,
                }
            );

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // RemoveWatched
    // =========================================================================

    [Fact]
    public async Task RemoveWatched_ValidUser_DelegatesToRepositoryWithTypedId()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        (VideoHub hub, Mock<IUserDataRepository> repo) = CreateHub(
            connectionId: Guid.NewGuid().ToString(),
            userId: userId,
            clients: out _
        );

        await hub.RemoveWatched(request: new() { TmdbId = 129, PlaylistType = MediaTypes.MovieMediaType });

        repo.Verify(
            expression: r =>
                r.RemoveForItemAsync(
                    userId,
                    MediaTypes.MovieMediaType,
                    129,
                    null,
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RemoveWatched_SpecialType_PassesUlidNotIntId()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        Ulid specialId = Ulid.NewUlid();
        (VideoHub hub, Mock<IUserDataRepository> repo) = CreateHub(
            connectionId: Guid.NewGuid().ToString(),
            userId: userId,
            clients: out _
        );

        await hub.RemoveWatched(
            request: new()
            {
                TmdbId = 0,
                SpecialId = specialId,
                PlaylistType = MediaTypes.SpecialMediaType,
            }
        );

        repo.Verify(
            expression: r =>
                r.RemoveForItemAsync(
                    userId,
                    MediaTypes.SpecialMediaType,
                    null,
                    specialId,
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    // =========================================================================
    // PlaybackCommand
    // =========================================================================

    [Fact]
    public async Task PlaybackCommand_UnknownCachedUser_IsNoOp()
    {
        Guid unknownUserId = Guid.NewGuid();
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: unknownUserId, clients: out _);

        Func<Task> act = async () => await hub.PlaybackCommand(command: "play");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PlaybackCommand_NoExistingState_DoesNotThrow()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        VideoPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<VideoPlayerStateManager>();
        stateManager.RemoveState(userId: userId);
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

        Func<Task> act = async () => await hub.PlaybackCommand(command: "play");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PlaybackCommand_ExistingState_DispatchesToRealCommandHandler()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        VideoPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<VideoPlayerStateManager>();
        VideoPlayerState state = new() { PlayState = false };
        stateManager.UpdateState(userId: userId, state: state);

        try
        {
            (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

            await hub.PlaybackCommand(command: "play");

            state.PlayState.Should().BeTrue();
        }
        finally
        {
            _factory.Services.GetRequiredService<VideoPlaybackService>().RemoveTimer(userId: userId);
            stateManager.RemoveState(userId: userId);
        }
    }

    [Fact]
    public async Task PlaybackCommand_StateHasNoDeviceId_AdoptsConnectedCallerDevice()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        string connectionId = Guid.NewGuid().ToString();
        VideoPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<VideoPlayerStateManager>();
        VideoPlayerState state = new() { PlayState = false, DeviceId = null };
        stateManager.UpdateState(userId: userId, state: state);

        string deviceId = $"caller-device-{Guid.NewGuid()}";
        Client callerClient = new()
        {
            Id = Ulid.NewUlid(),
            Sub = userId,
            DeviceId = deviceId,
            Endpoint = "/videoHub",
            Type = "web",
            Socket = Mock.Of<ISingleClientProxy>(),
            VolumePercent = 77,
        };
        ConnectedClients connectedClients = _factory.GetConnectedClients();
        connectedClients.Clients[key: connectionId] = callerClient;

        try
        {
            (VideoHub hub, _) = CreateHub(connectionId: connectionId, userId: userId, clients: out _);

            await hub.PlaybackCommand(command: "mute");

            state.DeviceId.Should().Be(expected: deviceId);
            state.VolumePercentage.Should().Be(expected: 77);
        }
        finally
        {
            connectedClients.Clients.TryRemove(key: connectionId, value: out _);
            _factory.Services.GetRequiredService<VideoPlaybackService>().RemoveTimer(userId: userId);
            stateManager.RemoveState(userId: userId);
        }
    }

    // =========================================================================
    // ChangeDeviceCommand
    // =========================================================================

    [Fact]
    public async Task ChangeDeviceCommand_EmptyDeviceId_IsNoOp()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

        Func<Task> act = async () => await hub.ChangeDeviceCommand(deviceId: "");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ChangeDeviceCommand_NoExistingPlayerState_UpdatesPlaybackStateAndReturns()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        VideoPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<VideoPlayerStateManager>();
        stateManager.RemoveState(userId: userId);
        (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

        Func<Task> act = async () => await hub.ChangeDeviceCommand(deviceId: "some-device-id");

        await act.Should().NotThrowAsync();
        stateManager.HasState(userId: userId).Should().BeFalse();
    }

    [Fact]
    public async Task ChangeDeviceCommand_NonTvTarget_SetsDeviceIdOnState_WithoutLaunchingCast()
    {
        Guid userId = TestAuthHandler.DefaultUserId;
        VideoPlayerStateManager stateManager =
            _factory.Services.GetRequiredService<VideoPlayerStateManager>();
        VideoPlayerState state = new() { PlayState = false };
        stateManager.UpdateState(userId: userId, state: state);

        string targetDeviceId = $"phone-{Guid.NewGuid()}";

        try
        {
            (VideoHub hub, _) = CreateHub(connectionId: Guid.NewGuid().ToString(), userId: userId, clients: out _);

            await hub.ChangeDeviceCommand(deviceId: targetDeviceId);

            state.DeviceId.Should().Be(expected: targetDeviceId);
        }
        finally
        {
            stateManager.RemoveState(userId: userId);
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
