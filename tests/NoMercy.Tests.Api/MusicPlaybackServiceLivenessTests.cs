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
/// Covers the liveness-based active-device release: MusicPlaybackService must
/// author who is active by proof-of-life, not by "is the WebSocket still
/// connected" — the root cause of a device (backgrounded TV, crashed player,
/// anonymous Cast Receiver) holding a music session forever after it stops
/// actually producing audio.
/// </summary>
[Trait("Category", "Unit")]
public class MusicPlaybackServiceLivenessTests
{
    private static MusicPlayerState MakePlayingState(string? deviceId, DateTime lastHeartbeatUtc)
    {
        return new()
        {
            DeviceId = deviceId,
            PlayState = true,
            CurrentList = new("/music/albums/test", UriKind.Relative),
            LastActiveHeartbeatUtc = lastHeartbeatUtc,
        };
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

    // ── IsActiveDeviceStale ──────────────────────────────────────────────────

    [Fact]
    public void IsActiveDeviceStale_False_WhenHeartbeatIsRecent()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState("device-a", now.AddSeconds(-5));

        MusicPlaybackService.IsActiveDeviceStale(state, now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveDeviceStale_True_WhenHeartbeatExceedsTimeout()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(
            "device-a",
            now.AddMilliseconds(-(MusicPlaybackService.ActiveDeviceStaleTimeoutMs + 1))
        );

        MusicPlaybackService.IsActiveDeviceStale(state, now).Should().BeTrue();
    }

    [Fact]
    public void IsActiveDeviceStale_False_AtExactTimeoutBoundary()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(
            "device-a",
            now.AddMilliseconds(-MusicPlaybackService.ActiveDeviceStaleTimeoutMs)
        );

        MusicPlaybackService.IsActiveDeviceStale(state, now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveDeviceStale_False_WhenPaused_RegardlessOfHeartbeatAge()
    {
        // A deliberate pause/stop must never be treated as staleness —
        // MusicPlaybackCommandHandler.HandleStop relies on the active device
        // staying sticky through a stop to block hijack from a passive tap.
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState("device-a", now.AddHours(-1));
        state.PlayState = false;

        MusicPlaybackService.IsActiveDeviceStale(state, now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveDeviceStale_False_WhenNoActiveDeviceRecorded()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(null, now.AddHours(-1));

        MusicPlaybackService.IsActiveDeviceStale(state, now).Should().BeFalse();
    }

    // ── IsCallerTheActiveDevice ──────────────────────────────────────────────

    [Fact]
    public void IsCallerTheActiveDevice_True_WhenCallerMatchesActiveDeviceId()
    {
        MusicPlayerState state = MakePlayingState("device-a", DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state, "device-a").Should().BeTrue();
    }

    [Fact]
    public void IsCallerTheActiveDevice_True_IsCaseInsensitive()
    {
        MusicPlayerState state = MakePlayingState("Device-A", DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state, "device-a").Should().BeTrue();
    }

    [Fact]
    public void IsCallerTheActiveDevice_False_WhenCallerIsAPassiveDevice()
    {
        // A stray report from a device that is NOT active must never refresh
        // the real active device's clock — that would mask a truly-dead device.
        MusicPlayerState state = MakePlayingState("device-a", DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state, "device-b").Should().BeFalse();
    }

    [Fact]
    public void IsCallerTheActiveDevice_False_WhenCallerDeviceIdIsNull()
    {
        MusicPlayerState state = MakePlayingState("device-a", DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state, null).Should().BeFalse();
    }

    [Fact]
    public void IsCallerTheActiveDevice_False_WhenNoActiveDeviceRecorded()
    {
        MusicPlayerState state = MakePlayingState(null, DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state, "device-a").Should().BeFalse();
    }

    // ── EndStaleActiveSessionAsync ───────────────────────────────────────────

    [Fact]
    public async Task EndStaleActiveSessionAsync_ClearsSessionAndReleasesActiveDevice()
    {
        (
            MusicPlaybackService service,
            _,
            MusicActiveDeviceRegistry registry,
            Mock<IClientMessenger> messenger
        ) = MakeService();

        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };
        registry.Set(userId, new() { DeviceId = "stale-device", Type = "web" });

        MusicPlayerState state = MakePlayingState("stale-device", DateTime.UtcNow.AddSeconds(-30));
        state.CurrentItem = MakeTrack();
        state.Playlist = [MakeTrack()];
        state.Backlog = [MakeTrack()];

        await service.EndStaleActiveSessionAsync(user, state);

        state.CurrentItem.Should().BeNull();
        state.PlayState.Should().BeFalse();
        state.DeviceId.Should().BeNull();
        state.Time.Should().Be(0);
        state.Playlist.Should().BeEmpty();
        state.Backlog.Should().BeEmpty();
        state.Actions.Disallows.Resuming.Should().BeTrue();
        state.Actions.Disallows.Pausing.Should().BeTrue();

        registry.TryGet(userId, out _).Should().BeFalse();

        messenger.Verify(
            m => m.SendTo("MusicPlayerState", "musicHub", userId, It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task EndStaleActiveSessionAsync_DoesNotReleaseADeviceThatAlreadyTookOver()
    {
        // A device switch (ChangeDeviceCommand) raced in between the staleness
        // detection and the release actually running — the switch must win.
        (MusicPlaybackService service, _, MusicActiveDeviceRegistry registry, _) = MakeService();

        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };
        registry.Set(userId, new() { DeviceId = "new-device", Type = "web" });

        MusicPlayerState state = MakePlayingState("stale-device", DateTime.UtcNow.AddSeconds(-30));
        state.CurrentItem = MakeTrack();

        await service.EndStaleActiveSessionAsync(user, state);

        registry.TryGet(userId, out Device? found).Should().BeTrue();
        found!.DeviceId.Should().Be("new-device");
    }

    // ── Real timer wiring ────────────────────────────────────────────────────
    // Proves StartPlaybackTimer's own tick actually reaches the staleness
    // sweep end to end, not just that the extracted predicate is correct in
    // isolation. Pre-ages the heartbeat past the timeout BEFORE the timer
    // starts so the very first ~100ms tick already observes it as stale —
    // no need to wait out the real 15s window.

    [Fact]
    public async Task StartPlaybackTimer_EndsSession_WhenActiveDeviceIsAlreadyStaleAtStart()
    {
        (
            MusicPlaybackService service,
            MusicPlayerStateManager stateManager,
            MusicActiveDeviceRegistry registry,
            _
        ) = MakeService();

        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };
        registry.Set(userId, new() { DeviceId = "zombie-device", Type = "web" });

        MusicPlayerState state = MakePlayingState("zombie-device", DateTime.UtcNow);
        state.CurrentItem = MakeTrack();
        stateManager.UpdateState(userId, state);

        try
        {
            // StartPlaybackTimer itself stamps a fresh heartbeat (that's the
            // resume-after-pause fix below), so back-date it AFTER starting —
            // simulating time passing with no further position reports, the
            // actual real-world staleness path — rather than fighting that stamp.
            service.StartPlaybackTimer(user);
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddMilliseconds(
                -(MusicPlaybackService.ActiveDeviceStaleTimeoutMs + 1_000)
            );

            MusicPlayerState? observed = null;
            for (
                int attempt = 0;
                attempt < 40 && (observed is null || observed.CurrentItem is not null);
                attempt++
            )
            {
                await Task.Delay(50);
                stateManager.TryGetValue(userId, out observed);
            }

            observed.Should().NotBeNull();
            observed!.CurrentItem.Should().BeNull();
            observed.PlayState.Should().BeFalse();
            registry.TryGet(userId, out _).Should().BeFalse();
        }
        finally
        {
            service.RemoveTimer(userId);
        }
    }

    [Fact]
    public async Task StartPlaybackTimer_ResetsHeartbeat_SoAResumeAfterALongPauseSurvives()
    {
        // Regression guard: a legitimate resume after sitting paused for far
        // longer than the stale timeout must NOT immediately look stale to the
        // very next tick — StartPlaybackTimer must stamp a fresh heartbeat
        // whenever it (re)arms the loop.
        (
            MusicPlaybackService service,
            MusicPlayerStateManager stateManager,
            MusicActiveDeviceRegistry registry,
            _
        ) = MakeService();

        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };
        registry.Set(userId, new() { DeviceId = "device-a", Type = "web" });

        MusicPlayerState state = MakePlayingState(
            "device-a",
            DateTime.UtcNow.AddHours(-1) // long stale-looking pause
        );
        state.CurrentItem = MakeTrack();
        stateManager.UpdateState(userId, state);

        try
        {
            service.StartPlaybackTimer(user);

            await Task.Delay(300);

            stateManager.TryGetValue(userId, out MusicPlayerState? observed);
            observed.Should().NotBeNull();
            observed!
                .CurrentItem.Should()
                .NotBeNull("resuming after a normal pause must not end the session");
            observed.PlayState.Should().BeTrue();
        }
        finally
        {
            service.RemoveTimer(userId);
        }
    }
}
