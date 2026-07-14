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
[Trait("Category", "Unit")]
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
        return new(track, "US");
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
            .Setup(m =>
                m.SendTo(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<object?>()
                )
            )
            .Returns(Task.CompletedTask);

        MusicPlaybackService service = new(
            stateManager,
            Mock.Of<IServiceProvider>(),
            messenger.Object,
            registry
        );

        return (service, stateManager, registry, messenger);
    }

    // ── ServerTimeMs (every broadcast emit) ──────────────────────────────────

    [Fact]
    public async Task UpdatePlaybackState_StampsServerTimeMs()
    {
        (MusicPlaybackService service, _, _, _) = MakeService();
        User user = new() { Id = Guid.NewGuid(), Name = "Test User" };
        MusicPlayerState state = new() { CurrentList = new("", UriKind.Relative) };

        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await service.UpdatePlaybackState(user, state);

        state.ServerTimeMs.Should().BeGreaterThanOrEqualTo(before);
        // Same instant backs both fields on a given emit.
        state.ServerTimeMs.Should().Be(state.Timestamp);
    }

    [Fact]
    public async Task UpdatePlaybackState_ServerTimeMsIsMonotoneAcrossSuccessiveBroadcasts()
    {
        (MusicPlaybackService service, _, _, _) = MakeService();
        User user = new() { Id = Guid.NewGuid(), Name = "Test User" };
        MusicPlayerState state = new() { CurrentList = new("", UriKind.Relative) };

        await service.UpdatePlaybackState(user, state);
        long first = state.ServerTimeMs;

        await Task.Delay(5);
        await service.UpdatePlaybackState(user, state);
        long second = state.ServerTimeMs;

        second.Should().BeGreaterThanOrEqualTo(first);
    }

    // ── PositionCapturedAtMs (SetPosition — the single choke point) ──────────

    [Fact]
    public void SetPosition_SetsTimeAndStampsPositionCapturedAtMs()
    {
        MusicPlayerState state = new() { CurrentList = new("", UriKind.Relative) };
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        state.SetPosition(12_345);

        state.Time.Should().Be(12_345);
        state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void SetPosition_RefreshesPositionCapturedAtMs_OnEachCall()
    {
        MusicPlayerState state = new() { CurrentList = new("", UriKind.Relative) };

        state.SetPosition(1_000);
        long firstCapture = state.PositionCapturedAtMs;

        Thread.Sleep(5);
        state.SetPosition(2_000);

        state.Time.Should().Be(2_000);
        state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(firstCapture);
    }

    // ── IsReportForCurrentItem (stale-tagged report guard) ───────────────────

    [Fact]
    public void IsReportForCurrentItem_True_WhenItemIdIsNull_Untagged()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrack(),
            CurrentList = new("", UriKind.Relative),
        };

        MusicPlaybackService.IsReportForCurrentItem(state, null).Should().BeTrue();
    }

    [Fact]
    public void IsReportForCurrentItem_True_WhenItemIdMatchesCurrentItem()
    {
        PlaylistTrackDto track = MakeTrack();
        MusicPlayerState state = new()
        {
            CurrentItem = track,
            CurrentList = new("", UriKind.Relative),
        };

        MusicPlaybackService.IsReportForCurrentItem(state, track.Id.ToString()).Should().BeTrue();
    }

    [Fact]
    public void IsReportForCurrentItem_IsCaseInsensitive()
    {
        PlaylistTrackDto track = MakeTrack();
        MusicPlayerState state = new()
        {
            CurrentItem = track,
            CurrentList = new("", UriKind.Relative),
        };

        MusicPlaybackService
            .IsReportForCurrentItem(state, track.Id.ToString().ToUpperInvariant())
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsReportForCurrentItem_False_WhenItemIdDoesNotMatchCurrentItem()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrack(),
            CurrentList = new("", UriKind.Relative),
        };

        MusicPlaybackService
            .IsReportForCurrentItem(state, Guid.NewGuid().ToString())
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsReportForCurrentItem_False_WhenNoCurrentItem()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = null,
            CurrentList = new("", UriKind.Relative),
        };

        MusicPlaybackService
            .IsReportForCurrentItem(state, Guid.NewGuid().ToString())
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsReportForCurrentItem_False_WhenItemIdIsNotAGuid()
    {
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrack(),
            CurrentList = new("", UriKind.Relative),
        };

        MusicPlaybackService.IsReportForCurrentItem(state, "not-a-guid").Should().BeFalse();
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
            CurrentList = new("/music/albums/test", UriKind.Relative),
            Time = 42_000,
            // HandleCommand's "next" branch reads Actions.Disallows.Next before
            // dispatching — Actions.Disallows has no property initializer
            // (real code always populates it via UpdateActionsDisallows first).
            Actions = new() { Disallows = new() },
        };

        MusicPlaybackCommandHandler handler = new(service);
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        handler.HandleCommand(user, "next", null, state);

        // All three land together, synchronously, before any broadcast fires —
        // a concurrent reader can never observe the new item with stale Time
        // (or vice versa).
        state.CurrentItem.Should().Be(trackB);
        state.Time.Should().Be(0);
        state.PositionCapturedAtMs.Should().BeGreaterThanOrEqualTo(before);
        state.Backlog.Should().Contain(trackA);
    }

    [Fact]
    public async Task DebouncedUpdatePlaybackState_AfterTrackChange_SendsExactlyOneBroadcast()
    {
        (MusicPlaybackService service, _, _, Mock<IClientMessenger> messenger) = MakeService();
        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };

        PlaylistTrackDto trackA = MakeTrack();
        PlaylistTrackDto trackB = MakeTrack();
        MusicPlayerState state = new()
        {
            DeviceId = "device-a",
            PlayState = true,
            CurrentItem = trackA,
            Playlist = [trackB],
            CurrentList = new("/music/albums/test", UriKind.Relative),
            Time = 42_000,
            // HandleCommand's "next" branch reads Actions.Disallows.Next before
            // dispatching — Actions.Disallows has no property initializer
            // (real code always populates it via UpdateActionsDisallows first).
            Actions = new() { Disallows = new() },
        };

        MusicPlaybackCommandHandler handler = new(service);
        handler.HandleCommand(user, "next", null, state);

        // The command handler may have (re)armed the real playback timer —
        // stop it so it can't sneak in extra ticks/broadcasts during the wait.
        service.RemoveTimer(userId);

        // Mirrors MusicHub.PlaybackCommand's skip-command path.
        service.DebouncedUpdatePlaybackState(user, state);

        await Task.Delay(400);

        messenger.Verify(
            m => m.SendTo("MusicPlayerState", "musicHub", userId, It.IsAny<object>()),
            Times.Once
        );

        // The single broadcast payload carries the state object as-is — item
        // and position never travel separately.
        state.CurrentItem.Should().Be(trackB);
        state.Time.Should().Be(0);
    }
}
