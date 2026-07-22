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

using Moq;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Messaging;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Covers the millisecond-class cross-device sync protocol: MusicPlayerState's
/// ServerTimeMs (stamped every broadcast emit) and PositionCapturedAtMs (stamped
/// whenever Time is authored — accepted report, seek, transfer, track change),
/// the item-tagged stale-report guard (<see cref="MusicPlaybackService.IsReportForCurrentItem"/>),
/// and the atomicity of a track change: item + Time + PositionCapturedAtMs must
/// land in the same synchronous mutation and travel on exactly one broadcast.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class MusicSyncProtocolTests
{
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

    private static (
        MusicPlaybackService Service,
        MusicPlayerStateManager StateManager,
        MusicActiveDeviceRegistry Registry,
        Mock<IClientMessenger> Messenger
    ) MakeService()
    {
        MusicPlayerStateManager stateManager = new();
        MusicActiveDeviceRegistry registry = new();
        Mock<IClientMessenger> messenger = new();
        messenger
            .Setup(expression: m =>
                m.SendTo(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<object?>()
                )
            )
            .Returns(value: Task.CompletedTask);

        MusicPlaybackService service = new(
            stateManager: stateManager,
            serviceProvider: Mock.Of<IServiceProvider>(),
            clientMessenger: messenger.Object,
            activeDeviceRegistry: registry
        );

        return (service, stateManager, registry, messenger);
    }

    // ── ServerTimeMs (every broadcast emit) ──────────────────────────────────

    [Fact]
    public async Task UpdatePlaybackState_StampsServerTimeMs()
    {
        (MusicPlaybackService service, _, _, _) = MakeService();
        User user = new() { Id = Guid.NewGuid(), Name = "Test User" };
        MusicPlayerState state = new() { CurrentList = new(uriString: "", uriKind: UriKind.Relative) };

        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await service.UpdatePlaybackState(user: user, state: state);

        state.ServerTimeMs.Should().BeGreaterThanOrEqualTo(expected: before);
        // Same instant backs both fields on a given emit.
        state.ServerTimeMs.Should().Be(expected: state.Timestamp);
    }

    [Fact]
    public async Task UpdatePlaybackState_ServerTimeMsIsMonotoneAcrossSuccessiveBroadcasts()
    {
        (MusicPlaybackService service, _, _, _) = MakeService();
        User user = new() { Id = Guid.NewGuid(), Name = "Test User" };
        MusicPlayerState state = new() { CurrentList = new(uriString: "", uriKind: UriKind.Relative) };

        await service.UpdatePlaybackState(user: user, state: state);
        long first = state.ServerTimeMs;

        await Task.Delay(millisecondsDelay: 5);
        await service.UpdatePlaybackState(user: user, state: state);
        long second = state.ServerTimeMs;

        second.Should().BeGreaterThanOrEqualTo(expected: first);
    }

    // ── PositionCapturedAtMs (SetPosition — the single choke point) ──────────

    [Fact]
    public void SetPosition_SetsTimeAndStampsPositionCapturedAtMs()
    {
        MusicPlayerState state = new() { CurrentList = new(uriString: "", uriKind: UriKind.Relative) };
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        state.SetPosition(positionMs: 12_345);

        state.Time.Should().Be(expected: 12_345);
        state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(expected: before);
    }

    [Fact]
    public void SetPosition_RefreshesPositionCapturedAtMs_OnEachCall()
    {
        MusicPlayerState state = new() { CurrentList = new(uriString: "", uriKind: UriKind.Relative) };

        state.SetPosition(positionMs: 1_000);
        long firstCapture = state.PositionCapturedAtMs;

        Thread.Sleep(millisecondsTimeout: 5);
        state.SetPosition(positionMs: 2_000);

        state.Time.Should().Be(expected: 2_000);
        state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(expected: firstCapture);
    }

    // ── IsReportForCurrentItem (stale-tagged report guard) ───────────────────

    [Fact]
    public void IsReportForCurrentItem_True_WhenItemIdIsNull_Untagged()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrack(),
            CurrentList = new(uriString: "", uriKind: UriKind.Relative),
        };

        MusicPlaybackService.IsReportForCurrentItem(state: state, itemId: null).Should().BeTrue();
    }

    [Fact]
    public void IsReportForCurrentItem_True_WhenItemIdMatchesCurrentItem()
    {
        PlaylistTrackDto track = MakeTrack();
        MusicPlayerState state = new()
        {
            CurrentItem = track,
            CurrentList = new(uriString: "", uriKind: UriKind.Relative),
        };

        MusicPlaybackService.IsReportForCurrentItem(state: state, itemId: track.Id.ToString()).Should().BeTrue();
    }

    [Fact]
    public void IsReportForCurrentItem_IsCaseInsensitive()
    {
        PlaylistTrackDto track = MakeTrack();
        MusicPlayerState state = new()
        {
            CurrentItem = track,
            CurrentList = new(uriString: "", uriKind: UriKind.Relative),
        };

        MusicPlaybackService
            .IsReportForCurrentItem(state: state, itemId: track.Id.ToString().ToUpperInvariant())
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsReportForCurrentItem_False_WhenItemIdDoesNotMatchCurrentItem()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrack(),
            CurrentList = new(uriString: "", uriKind: UriKind.Relative),
        };

        MusicPlaybackService
            .IsReportForCurrentItem(state: state, itemId: Guid.NewGuid().ToString())
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsReportForCurrentItem_False_WhenNoCurrentItem()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = null,
            CurrentList = new(uriString: "", uriKind: UriKind.Relative),
        };

        MusicPlaybackService
            .IsReportForCurrentItem(state: state, itemId: Guid.NewGuid().ToString())
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsReportForCurrentItem_False_WhenItemIdIsNotAGuid()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrack(),
            CurrentList = new(uriString: "", uriKind: UriKind.Relative),
        };

        MusicPlaybackService.IsReportForCurrentItem(state: state, itemId: "not-a-guid").Should().BeFalse();
    }

    // ── Atomic track change: item + Time + PositionCapturedAtMs together, one broadcast ──

    [Fact]
    public void HandleNext_AtomicallySetsItemAndPositionAndCaptureTimestamp()
    {
        (MusicPlaybackService service, _, _, _) = MakeService();
        User user = new() { Id = Guid.NewGuid(), Name = "Test User" };

        PlaylistTrackDto trackA = MakeTrack();
        PlaylistTrackDto trackB = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = "device-a",
            PlayState = true,
            CurrentItem = trackA,
            Playlist = [trackB],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
            Time = 42_000,
            // HandleCommand's "next" branch reads Actions.Disallows.Next before
            // dispatching — Actions.Disallows has no property initializer
            // (real code always populates it via UpdateActionsDisallows first).
            Actions = new() { Disallows = new() },
        };

        MusicPlaybackCommandHandler handler = new(musicPlaybackService: service);
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        handler.HandleCommand(user: user, command: "next", data: null, state: state);

        // All three land together, synchronously, before any broadcast fires —
        // a concurrent reader can never observe the new item with stale Time
        // (or vice versa).
        state.CurrentItem.Should().Be(expected: trackB);
        state.Time.Should().Be(expected: 0);
        state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(expected: before);
        state.Backlog.Should().Contain(expected: trackA);
    }

    [Fact]
    public async Task DebouncedUpdatePlaybackState_AfterTrackChange_SendsExactlyOneBroadcast()
    {
        (MusicPlaybackService service, _, _, Mock<IClientMessenger> messenger) = MakeService();
        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };

        // Count the debounced broadcast deterministically instead of racing a
        // fixed wall-clock wait against the 150ms debounce timer. Under parallel
        // test load a starved timer callback can land well after a fixed delay,
        // which flaked the old `Task.Delay(400)` + Times.Once assertion. Signal on
        // the first broadcast and await it with a generous timeout instead.
        int broadcasts = 0;
        TaskCompletionSource firstBroadcast = new(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
        );
        messenger
            .Setup(expression: m => m.SendTo("MusicPlayerState", "musicHub", userId, It.IsAny<object?>()))
            .Callback(action: () =>
            {
                if (Interlocked.Increment(location: ref broadcasts) == 1)
                    firstBroadcast.TrySetResult();
            })
            .Returns(value: Task.CompletedTask);

        PlaylistTrackDto trackA = MakeTrack();
        PlaylistTrackDto trackB = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = "device-a",
            PlayState = true,
            CurrentItem = trackA,
            Playlist = [trackB],
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
            Time = 42_000,
            // HandleCommand's "next" branch reads Actions.Disallows.Next before
            // dispatching — Actions.Disallows has no property initializer
            // (real code always populates it via UpdateActionsDisallows first).
            Actions = new() { Disallows = new() },
        };

        MusicPlaybackCommandHandler handler = new(musicPlaybackService: service);
        handler.HandleCommand(user: user, command: "next", data: null, state: state);

        // The command handler may have (re)armed the real playback timer —
        // stop it so it can't sneak in extra ticks/broadcasts during the wait.
        service.RemoveTimer(userId: userId);

        // Mirrors MusicHub.PlaybackCommand's skip-command path.
        service.DebouncedUpdatePlaybackState(user: user, state: state);

        // Absorb thread-pool starvation: a missing broadcast throws TimeoutException.
        await firstBroadcast.Task.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));

        // An erroneous second broadcast would arrive within another debounce
        // window (150ms); settle briefly, then confirm the debounce coalesced.
        await Task.Delay(millisecondsDelay: 200);
        broadcasts.Should().Be(expected: 1);

        // The single broadcast payload carries the state object as-is — item
        // and position never travel separately.
        state.CurrentItem.Should().Be(expected: trackB);
        state.Time.Should().Be(expected: 0);
    }
}
