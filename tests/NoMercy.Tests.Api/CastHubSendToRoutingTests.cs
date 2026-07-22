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
using NoMercy.Networking.Cast;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Auth;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

// The 25 CastHub methods below (Play/Pause/Time/Ended/Volume/Muted/Item/
// Playlist/SubtitleTracks/CurrentSubtitleTrack/AudioTracks/CurrentAudioTrack/
// GetPlayerState/PlayerState/Set*) share one shape: resolve the caller's
// cached user, no-op when unresolved, otherwise forward a specific event name
// through IClientMessenger.SendTo to that user's other connections. The event
// NAME is the actual wire contract clients switch on — a silent rename (e.g.
// PlayerState's "MusicPlayerState" send) breaks every already-deployed client
// without a compile error anywhere. These tests build a real CastHub against
// the app's actual DI-configured MediaContext/UserCache (via NoMercyApiFactory)
// with IClientMessenger mocked — no real cast/broadcast ever happens.
[Trait(name: "Category", value: "Characterization")]
public class CastHubSendToRoutingTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public CastHubSendToRoutingTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        // Force the test host to start so UserCache.Current is populated with
        // the seeded DefaultUserId.
        _factory.CreateClient();
    }

    private CastHub CreateHub(Guid callerUserId, out Mock<IClientMessenger> clientMessenger)
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();

        clientMessenger = new Mock<IClientMessenger>();

        CastHub hub = new(
            logger: NullLogger<CastHub>.Instance,
            httpContextAccessor: Mock.Of<IHttpContextAccessor>(),
            contextFactory: contextFactory,
            connectedClients: new ConnectedClients(),
            clientMessenger: clientMessenger.Object,
            activityLogger: Mock.Of<IActivityLogger>(),
            authTokenStore: Mock.Of<IAuthTokenStore>(),
            chromeCast: Mock.Of<IChromeCastService>()
        );

        ClaimsPrincipal principal = new(
            identity: new ClaimsIdentity(
                claims: [new(type: ClaimTypes.NameIdentifier, value: callerUserId.ToString())],
                authenticationType: "TestAuth"
            )
        );

        Mock<HubCallerContext> context = new();
        context.Setup(expression: c => c.User).Returns(value: principal);
        context.Setup(expression: c => c.ConnectionId).Returns(value: Guid.NewGuid().ToString());
        context.Setup(expression: c => c.ConnectionAborted).Returns(value: CancellationToken.None);

        hub.Context = context.Object;
        hub.Clients = Mock.Of<IHubCallerClients>();

        return hub;
    }

    public static IEnumerable<object[]> RoutedMethods()
    {
        yield return new object[] { "Play", (Func<CastHub, Task>)(h => h.Play()) };
        yield return new object[] { "Pause", (Func<CastHub, Task>)(h => h.Pause()) };
        yield return new object[]
        {
            "Time",
            (Func<CastHub, Task>)(h => h.Time(time: new CastHub.TimeData())),
        };
        yield return new object[] { "Ended", (Func<CastHub, Task>)(h => h.Ended()) };
        yield return new object[] { "Volume", (Func<CastHub, Task>)(h => h.Volume(volume: 50)) };
        yield return new object[] { "Muted", (Func<CastHub, Task>)(h => h.Muted(muted: true)) };
        yield return new object[]
        {
            "Item",
            (Func<CastHub, Task>)(h => h.Item(item: new CastHub.PlaylistItem())),
        };
        yield return new object[] { "Playlist", (Func<CastHub, Task>)(h => h.Playlist(item: [])) };
        yield return new object[]
        {
            "SubtitleTracks",
            (Func<CastHub, Task>)(h => h.SubtitleTracks(subtitleTracks: [])),
        };
        yield return new object[]
        {
            "CurrentSubtitleTrack",
            (Func<CastHub, Task>)(h => h.CurrentSubtitleTrack(subtitleTrack: new CastHub.TextTrack())),
        };
        yield return new object[] { "AudioTracks", (Func<CastHub, Task>)(h => h.AudioTracks(audioTrack: [])) };
        yield return new object[]
        {
            "CurrentAudioTrack",
            (Func<CastHub, Task>)(h => h.CurrentAudioTrack(audioTrack: new CastHub.AudioTrack())),
        };
        yield return new object[]
        {
            "GetPlayerState",
            (Func<CastHub, Task>)(h => h.GetPlayerState()),
        };
        yield return new object[]
        {
            // PlayerState the METHOD forwards a DIFFERENT event name than its own
            // name — this mapping is the entire point of the test.
            "MusicPlayerState",
            (Func<CastHub, Task>)(h => h.PlayerState(state: new CastHub.CastPlayerState())),
        };
        yield return new object[]
        {
            "SetAudioTrack",
            (Func<CastHub, Task>)(h => h.SetAudioTrack(audioTrack: 1)),
        };
        yield return new object[]
        {
            "SetSubtitleTrack",
            (Func<CastHub, Task>)(h => h.SetSubtitleTrack(subtitleTrack: 1)),
        };
        yield return new object[]
        {
            "SetPlaylistItem",
            (Func<CastHub, Task>)(h => h.SetPlaylistItem(item: 1)),
        };
        yield return new object[] { "SetVolume", (Func<CastHub, Task>)(h => h.SetVolume(volume: 50)) };
        yield return new object[] { "SetMuted", (Func<CastHub, Task>)(h => h.SetMuted(muted: true)) };
        yield return new object[] { "SetSeek", (Func<CastHub, Task>)(h => h.SetSeek(time: 10)) };
        yield return new object[] { "SetNext", (Func<CastHub, Task>)(h => h.SetNext()) };
        yield return new object[] { "SetPrevious", (Func<CastHub, Task>)(h => h.SetPrevious()) };
        yield return new object[] { "SetPlay", (Func<CastHub, Task>)(h => h.SetPlay()) };
        yield return new object[] { "SetPause", (Func<CastHub, Task>)(h => h.SetPause()) };
        yield return new object[] { "SetStop", (Func<CastHub, Task>)(h => h.SetStop()) };
    }

    [Theory]
    [MemberData(memberName: nameof(RoutedMethods))]
    public async Task RoutedMethod_ForwardsExpectedEventName_ToCallerUser(
        string expectedEventName,
        Func<CastHub, Task> invoke
    )
    {
        CastHub hub = CreateHub(
            callerUserId: TestAuthHandler.DefaultUserId,
            clientMessenger: out Mock<IClientMessenger> clientMessenger
        );

        await invoke(arg: hub);

        clientMessenger.Verify(
            expression: m =>
                m.SendTo(
                    expectedEventName,
                    "castHub",
                    TestAuthHandler.DefaultUserId,
                    It.IsAny<object?>()
                ),
            times: Times.Once
        );
    }

    [Theory]
    [MemberData(memberName: nameof(RoutedMethods))]
    public async Task RoutedMethod_IsNoOp_WhenCallerUserIsNotCached(
        string expectedEventName,
        Func<CastHub, Task> invoke
    )
    {
        _ = expectedEventName;
        CastHub hub = CreateHub(callerUserId: Guid.NewGuid(), clientMessenger: out Mock<IClientMessenger> clientMessenger);

        await invoke(arg: hub);

        clientMessenger.Verify(
            expression: m =>
                m.SendTo(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<object?>()
                ),
            times: Times.Never
        );
    }
}
