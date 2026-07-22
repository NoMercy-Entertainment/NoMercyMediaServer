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
[Trait(name: "Category", value: "Unit")]
public class MusicPlaybackServiceLivenessTests
{
    private static MusicPlayerState MakePlayingState(string? deviceId, DateTime lastHeartbeatUtc)
    {
        return new()
        {
            DeviceId = deviceId,
            PlayState = true,
            CurrentList = new(uriString: "/music/albums/test", uriKind: UriKind.Relative),
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

    // ── IsActiveDeviceStale ──────────────────────────────────────────────────

    [Fact]
    public void IsActiveDeviceStale_False_WhenHeartbeatIsRecent()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: now.AddSeconds(value: -5));

        MusicPlaybackService.IsActiveDeviceStale(state: state, nowUtc: now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveDeviceStale_True_WhenHeartbeatExceedsTimeout()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(
            deviceId: "device-a",
            lastHeartbeatUtc: now.AddMilliseconds(value: -(MusicPlaybackService.ActiveDeviceStaleTimeoutMs + 1))
        );

        MusicPlaybackService.IsActiveDeviceStale(state: state, nowUtc: now).Should().BeTrue();
    }

    [Fact]
    public void IsActiveDeviceStale_False_AtExactTimeoutBoundary()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(
            deviceId: "device-a",
            lastHeartbeatUtc: now.AddMilliseconds(value: -MusicPlaybackService.ActiveDeviceStaleTimeoutMs)
        );

        MusicPlaybackService.IsActiveDeviceStale(state: state, nowUtc: now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveDeviceStale_False_WhenPaused_RegardlessOfHeartbeatAge()
    {
        // A deliberate pause/stop must never be treated as staleness —
        // MusicPlaybackCommandHandler.HandleStop relies on the active device
        // staying sticky through a stop to block hijack from a passive tap.
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: now.AddHours(value: -1));
        state.PlayState = false;

        MusicPlaybackService.IsActiveDeviceStale(state: state, nowUtc: now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveDeviceStale_False_WhenNoActiveDeviceRecorded()
    {
        DateTime now = DateTime.UtcNow;
        MusicPlayerState state = MakePlayingState(deviceId: null, lastHeartbeatUtc: now.AddHours(value: -1));

        MusicPlaybackService.IsActiveDeviceStale(state: state, nowUtc: now).Should().BeFalse();
    }

    // ── IsCallerTheActiveDevice ──────────────────────────────────────────────

    [Fact]
    public void IsCallerTheActiveDevice_True_WhenCallerMatchesActiveDeviceId()
    {
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state: state, callerDeviceId: "device-a").Should().BeTrue();
    }

    [Fact]
    public void IsCallerTheActiveDevice_True_IsCaseInsensitive()
    {
        MusicPlayerState state = MakePlayingState(deviceId: "Device-A", lastHeartbeatUtc: DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state: state, callerDeviceId: "device-a").Should().BeTrue();
    }

    [Fact]
    public void IsCallerTheActiveDevice_False_WhenCallerIsAPassiveDevice()
    {
        // A stray report from a device that is NOT active must never refresh
        // the real active device's clock — that would mask a truly-dead device.
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state: state, callerDeviceId: "device-b").Should().BeFalse();
    }

    [Fact]
    public void IsCallerTheActiveDevice_False_WhenCallerDeviceIdIsNull()
    {
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state: state, callerDeviceId: null).Should().BeFalse();
    }

    [Fact]
    public void IsCallerTheActiveDevice_False_WhenNoActiveDeviceRecorded()
    {
        MusicPlayerState state = MakePlayingState(deviceId: null, lastHeartbeatUtc: DateTime.UtcNow);

        MusicPlaybackService.IsCallerTheActiveDevice(state: state, callerDeviceId: "device-a").Should().BeFalse();
    }

    // ── TryRefreshHeartbeat ───────────────────────────────────────────────────
    // The single gate MusicHub routes ReportPositionCommand/CurrentTimeCommand,
    // PlaybackCommand, and GetStateCommand through. Covers both effects it must
    // have together: proof-of-life for the staleness sweep, and (via its bool
    // result) permission for the caller to move the authoritative position —
    // a passive caller must get neither.

    [Fact]
    public void TryRefreshHeartbeat_RefreshesHeartbeatAndReturnsTrue_WhenCallerIsActiveDevice()
    {
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: DateTime.UtcNow.AddSeconds(value: -10));

        bool result = MusicPlaybackService.TryRefreshHeartbeat(state: state, callerDeviceId: "device-a");

        result.Should().BeTrue();
        state.LastActiveHeartbeatUtc.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 1));
    }

    [Fact]
    public void TryRefreshHeartbeat_IsCaseInsensitive()
    {
        MusicPlayerState state = MakePlayingState(deviceId: "Device-A", lastHeartbeatUtc: DateTime.UtcNow.AddSeconds(value: -10));

        MusicPlaybackService.TryRefreshHeartbeat(state: state, callerDeviceId: "device-a").Should().BeTrue();
    }

    [Fact]
    public void TryRefreshHeartbeat_LeavesHeartbeatUntouched_AndReturnsFalse_WhenCallerIsPassive()
    {
        // A passive mirror's own report — e.g. a device that was just demoted by
        // ChangeDeviceCommand and hasn't yet stopped its own reporting loop — must
        // be a complete no-op: it may neither extend the active device's grace
        // window nor (per the caller's use of the false return) overwrite the
        // authoritative position with its own.
        DateTime lastHeartbeat = DateTime.UtcNow.AddSeconds(value: -10);
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: lastHeartbeat);

        MusicPlaybackService.TryRefreshHeartbeat(state: state, callerDeviceId: "device-b").Should().BeFalse();

        state.LastActiveHeartbeatUtc.Should().Be(expected: lastHeartbeat);
    }

    [Fact]
    public void TryRefreshHeartbeat_ReturnsFalse_WhenCallerDeviceIdIsNull()
    {
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: DateTime.UtcNow.AddSeconds(value: -10));

        MusicPlaybackService.TryRefreshHeartbeat(state: state, callerDeviceId: null).Should().BeFalse();
    }

    [Fact]
    public void TryRefreshHeartbeat_ReturnsFalse_WhenNoActiveDeviceRecorded()
    {
        MusicPlayerState state = MakePlayingState(deviceId: null, lastHeartbeatUtc: DateTime.UtcNow.AddSeconds(value: -10));

        MusicPlaybackService.TryRefreshHeartbeat(state: state, callerDeviceId: "device-a").Should().BeFalse();
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
        registry.Set(userId: userId, device: new() { DeviceId = "stale-device", Type = "web" });

        MusicPlayerState state = MakePlayingState(deviceId: "stale-device", lastHeartbeatUtc: DateTime.UtcNow.AddSeconds(value: -30));
        state.CurrentItem = MakeTrack();
        state.Playlist = [MakeTrack()];
        state.Backlog = [MakeTrack()];

        await service.EndStaleActiveSessionAsync(user: user, state: state);

        state.CurrentItem.Should().BeNull();
        state.PlayState.Should().BeFalse();
        state.DeviceId.Should().BeNull();
        state.Time.Should().Be(expected: 0);
        state.Playlist.Should().BeEmpty();
        state.Backlog.Should().BeEmpty();
        state.Actions.Disallows.Resuming.Should().BeTrue();
        state.Actions.Disallows.Pausing.Should().BeTrue();

        registry.TryGet(userId: userId, device: out _).Should().BeFalse();

        messenger.Verify(
            expression: m => m.SendTo("MusicPlayerState", "musicHub", userId, It.IsAny<object>()),
            times: Times.Once
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
        registry.Set(userId: userId, device: new() { DeviceId = "new-device", Type = "web" });

        MusicPlayerState state = MakePlayingState(deviceId: "stale-device", lastHeartbeatUtc: DateTime.UtcNow.AddSeconds(value: -30));
        state.CurrentItem = MakeTrack();

        await service.EndStaleActiveSessionAsync(user: user, state: state);

        registry.TryGet(userId: userId, device: out Device? found).Should().BeTrue();
        found!.DeviceId.Should().Be(expected: "new-device");
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
        registry.Set(userId: userId, device: new() { DeviceId = "zombie-device", Type = "web" });

        MusicPlayerState state = MakePlayingState(deviceId: "zombie-device", lastHeartbeatUtc: DateTime.UtcNow);
        state.CurrentItem = MakeTrack();
        stateManager.UpdateState(userId: userId, state: state);

        try
        {
            // StartPlaybackTimer itself stamps a fresh heartbeat (that's the
            // resume-after-pause fix below), so back-date it AFTER starting —
            // simulating time passing with no further position reports, the
            // actual real-world staleness path — rather than fighting that stamp.
            service.StartPlaybackTimer(user: user);
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddMilliseconds(
                value: -(MusicPlaybackService.ActiveDeviceStaleTimeoutMs + 1_000)
            );

            MusicPlayerState? observed = null;
            for (
                int attempt = 0;
                attempt < 40 && (observed is null || observed.CurrentItem is not null);
                attempt++
            )
            {
                await Task.Delay(millisecondsDelay: 50);
                stateManager.TryGetValue(userId: userId, state: out observed);
            }

            observed.Should().NotBeNull();
            observed!.CurrentItem.Should().BeNull();
            observed.PlayState.Should().BeFalse();
            registry.TryGet(userId: userId, device: out _).Should().BeFalse();
        }
        finally
        {
            service.RemoveTimer(userId: userId);
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
        registry.Set(userId: userId, device: new() { DeviceId = "device-a", Type = "web" });

        MusicPlayerState state = MakePlayingState(
            deviceId: "device-a",
            lastHeartbeatUtc: DateTime.UtcNow.AddHours(value: -1) // long stale-looking pause
        );
        state.CurrentItem = MakeTrack();
        stateManager.UpdateState(userId: userId, state: state);

        try
        {
            service.StartPlaybackTimer(user: user);

            await Task.Delay(millisecondsDelay: 300);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? observed);
            observed.Should().NotBeNull();
            observed!
                .CurrentItem.Should()
                .NotBeNull(because: "resuming after a normal pause must not end the session");
            observed.PlayState.Should().BeTrue();
        }
        finally
        {
            service.RemoveTimer(userId: userId);
        }
    }

    // ── BeginPlaybackStart / EndPlaybackStart (StartPlaybackCommand in-flight guard) ──
    // SignalR dispatches invocations on one connection serially by default, so a slow
    // playlist fetch inside StartPlaybackCommand starves that connection's queued
    // position reports for its whole duration — the watchdog must not kill the session
    // out from under a client that is still alive and simply queued behind its own
    // start command, but a genuinely dead device must still be caught once the flag
    // clears.

    [Fact]
    public void BeginPlaybackStart_StampsFreshHeartbeat_ForExistingSession()
    {
        (MusicPlaybackService service, MusicPlayerStateManager stateManager, _, _) = MakeService();

        Guid userId = Guid.NewGuid();
        MusicPlayerState state = MakePlayingState(deviceId: "device-a", lastHeartbeatUtc: DateTime.UtcNow.AddHours(value: -1));
        stateManager.UpdateState(userId: userId, state: state);

        service.BeginPlaybackStart(userId: userId);

        state.LastActiveHeartbeatUtc.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 2));

        service.EndPlaybackStart(userId: userId);
    }

    [Fact]
    public void IsPlaybackStartInFlight_FalseBeforeBegin_TrueAfterBegin_FalseAfterEnd()
    {
        (MusicPlaybackService service, _, _, _) = MakeService();
        Guid userId = Guid.NewGuid();

        service.IsPlaybackStartInFlight(userId: userId).Should().BeFalse();

        service.BeginPlaybackStart(userId: userId);
        service.IsPlaybackStartInFlight(userId: userId).Should().BeTrue();

        service.EndPlaybackStart(userId: userId);
        service.IsPlaybackStartInFlight(userId: userId).Should().BeFalse();
    }

    [Fact]
    public async Task StartPlaybackTimer_DoesNotEndSession_WhilePlaybackStartIsInFlight_EvenIfHeartbeatIsStale()
    {
        (
            MusicPlaybackService service,
            MusicPlayerStateManager stateManager,
            MusicActiveDeviceRegistry registry,
            _
        ) = MakeService();

        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };
        registry.Set(userId: userId, device: new() { DeviceId = "zombie-device", Type = "web" });

        MusicPlayerState state = MakePlayingState(deviceId: "zombie-device", lastHeartbeatUtc: DateTime.UtcNow);
        state.CurrentItem = MakeTrack();
        stateManager.UpdateState(userId: userId, state: state);

        try
        {
            service.BeginPlaybackStart(userId: userId);
            service.StartPlaybackTimer(user: user);

            // Simulate the long GetPlaylist fetch: no further reports arrive and the
            // heartbeat ages well past ActiveDeviceStaleTimeoutMs while the flag is set.
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddMilliseconds(
                value: -(MusicPlaybackService.ActiveDeviceStaleTimeoutMs + 1_000)
            );

            await Task.Delay(millisecondsDelay: 300);

            stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? observed);
            observed.Should().NotBeNull();
            observed!
                .CurrentItem.Should()
                .NotBeNull(because: "a start in flight must never look stale to the watchdog");
            observed.PlayState.Should().BeTrue();
            registry.TryGet(userId: userId, device: out _).Should().BeTrue();
        }
        finally
        {
            service.EndPlaybackStart(userId: userId);
            service.RemoveTimer(userId: userId);
        }
    }

    [Fact]
    public async Task StartPlaybackTimer_EndsSession_OnceInFlightFlagClears_IfStillStale()
    {
        // A genuinely dead device must still be caught on the very next cadence once
        // the in-flight flag clears — the guard above only defers detection for the
        // duration of a legitimate start, it must never suppress it permanently.
        (
            MusicPlaybackService service,
            MusicPlayerStateManager stateManager,
            MusicActiveDeviceRegistry registry,
            _
        ) = MakeService();

        Guid userId = Guid.NewGuid();
        User user = new() { Id = userId, Name = "Test User" };
        registry.Set(userId: userId, device: new() { DeviceId = "zombie-device", Type = "web" });

        MusicPlayerState state = MakePlayingState(deviceId: "zombie-device", lastHeartbeatUtc: DateTime.UtcNow);
        state.CurrentItem = MakeTrack();
        stateManager.UpdateState(userId: userId, state: state);

        try
        {
            service.BeginPlaybackStart(userId: userId);
            service.StartPlaybackTimer(user: user);
            state.LastActiveHeartbeatUtc = DateTime.UtcNow.AddMilliseconds(
                value: -(MusicPlaybackService.ActiveDeviceStaleTimeoutMs + 1_000)
            );

            // The flag clears — mirrors StartPlaybackCommand's finally block once its
            // (slow) work finishes without ever receiving a fresh report.
            service.EndPlaybackStart(userId: userId);

            MusicPlayerState? observed = null;
            for (
                int attempt = 0;
                attempt < 40 && (observed is null || observed.CurrentItem is not null);
                attempt++
            )
            {
                await Task.Delay(millisecondsDelay: 50);
                stateManager.TryGetValue(userId: userId, state: out observed);
            }

            observed.Should().NotBeNull();
            observed!.CurrentItem.Should().BeNull();
            observed.PlayState.Should().BeFalse();
            registry.TryGet(userId: userId, device: out _).Should().BeFalse();
        }
        finally
        {
            service.RemoveTimer(userId: userId);
        }
    }
}
