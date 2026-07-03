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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Playback;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Services.Music;

public class MusicPlaybackService
{
    private readonly MusicPlayerStateManager _stateManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClientMessenger _clientMessenger;
    private readonly IEventBus? _eventBus;
    private readonly MusicActiveDeviceRegistry _activeDeviceRegistry;
    private readonly ILogger<MusicPlaybackService>? _logger;
    private readonly string[] _repeatStates = ["off", "one", "all"];
    private static int PlayerStateEventId => Interlocked.Increment(ref field);

    public MusicPlaybackService(
        MusicPlayerStateManager stateManager,
        IServiceProvider serviceProvider,
        IClientMessenger clientMessenger,
        MusicActiveDeviceRegistry activeDeviceRegistry,
        IEventBus? eventBus = null,
        ILogger<MusicPlaybackService>? logger = null
    )
    {
        _stateManager = stateManager;
        _serviceProvider = serviceProvider;
        _clientMessenger = clientMessenger;
        _activeDeviceRegistry = activeDeviceRegistry;
        _eventBus = eventBus;
        _logger = logger;
    }

    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private readonly ConcurrentDictionary<Guid, Timer> _broadcastTimers = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _stateLocks = new();
    private const int TimerInterval = 100;
    private const int CrossfadeLeewayMs = 10000; // Send PrepareCrossfade 10s before track end (gives time to buffer)
    private const int BroadcastDebounceMs = 150;
    private const int MinPlayTimeForRecordMs = 10_000;

    // Both the Android and web clients throttle position reports to roughly once
    // every 5s while playing (MusicHubAdapter/MusicPlayerEventBridge on Android,
    // audioPlayer.ts on web all gate on ">5s since last send"). Three missed
    // reports tolerates normal network jitter or a dropped packet without a false
    // positive, while still recovering within one refresh cycle of a device that
    // actually went dark — a backgrounded/crashed player, or a Cast Receiver whose
    // WebSocket lingers after its cast session has effectively ended.
    internal const int ActiveDeviceStaleTimeoutMs = 15_000;

    // Safety margin added on top of the client-reported fadeDuration when computing
    // CrossfadeTimeout.  If CrossfadeComplete never arrives within that window the server
    // forces auto-advance anyway (handles client disconnect / network loss mid-crossfade).
    private const int CrossfadeSafetyMarginMs = 5000;

    private SemaphoreSlim GetStateLock(Guid userId)
    {
        return _stateLocks.GetOrAdd(userId, _ => new(1, 1));
    }

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(user.Id, out Timer? existingTimer))
            existingTimer.Dispose();

        if (!_stateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
            return;

        // Any (re)start of the ticking loop counts as proof of life, regardless of
        // who asked for it. Playback can be legitimately resumed remotely from any
        // connected device (not just the active one), so gating this on caller
        // identity would either reject a valid remote resume or reintroduce the
        // exact bug this exists to prevent: resuming after a long pause must not
        // make the active device look instantly stale to the check below.
        playerState.LastActiveHeartbeatUtc = DateTime.UtcNow;

        SemaphoreSlim stateLock = GetStateLock(user.Id);

        Timer timer = new(
            async _ =>
            {
                if (!await stateLock.WaitAsync(0))
                    return; // Skip tick if previous tick is still running
                try
                {
                    if (!_stateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
                        return;
                    if (!playerState.PlayState || playerState.CurrentItem is null)
                        return;

                    if (IsActiveDeviceStale(playerState, DateTime.UtcNow))
                    {
                        await EndStaleActiveSessionAsync(user, playerState);
                        return;
                    }

                    playerState.Time += TimerInterval;

                    int duration = playerState.CurrentItem.Duration.ToMilliSeconds();

                    if (
                        playerState.Time >= (duration / 2)
                        && playerState.Time < (duration / 2) + TimerInterval
                        && playerState.Time >= MinPlayTimeForRecordMs
                    )
                    {
                        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
                        IDbContextFactory<MediaContext> factory =
                            scope.ServiceProvider.GetRequiredService<
                                IDbContextFactory<MediaContext>
                            >();
                        await using MediaContext ctx = await factory.CreateDbContextAsync();
                        await ctx.MusicPlays.AddAsync(new(user.Id, playerState.CurrentItem.Id));
                        await ctx.SaveChangesAsync();
                        await PublishProgressEventAsync(user.Id, playerState);
                    }

                    // Send crossfade signal to clients ~10s before track ends
                    if (
                        playerState.Time >= duration - CrossfadeLeewayMs
                        && !playerState.CrossfadeSignalSent
                    )
                    {
                        playerState.CrossfadeSignalSent = true;
                        PlaylistTrackDto? nextItem = PeekNextTrack(playerState);
                        if (nextItem != null)
                            await SendPrepareCrossfadeSignal(user, nextItem);
                    }

                    if (playerState.Time >= duration)
                    {
                        // If a client has taken ownership of the crossfade, suppress server
                        // auto-advance and wait for CrossfadeComplete (or the safety timeout).
                        if (playerState.IsCrossfading)
                        {
                            if (DateTime.UtcNow < playerState.CrossfadeTimeout)
                                return; // Still within the allowed crossfade window — do nothing
                            // Safety timeout expired: client never sent CrossfadeComplete.
                            // Clear the crossfade flag and fall through to normal auto-advance.
                            playerState.IsCrossfading = false;
                            playerState.CrossfadeDeviceId = null;
                        }

                        await HandleTrackCompletion(user, playerState);
                    }
                }
                finally
                {
                    stateLock.Release();
                }
            },
            null,
            100,
            TimerInterval
        );

        _timers[user.Id] = timer;
    }

    public void RemoveTimer(Guid userId)
    {
        if (_timers.TryRemove(userId, out Timer? timer))
            timer.Dispose();
    }

    /// <summary>
    /// True once the session says it's playing but the active device hasn't proven
    /// life (see <see cref="StartPlaybackTimer"/> and
    /// <see cref="NoMercy.Api.Hubs.MusicHub.ReportPositionCommand"/>) for
    /// <see cref="ActiveDeviceStaleTimeoutMs"/>. Deliberately false while paused —
    /// a paused/stopped session has no reporting cadence to fall behind on, and
    /// <see cref="MusicPlaybackCommandHandler.HandleStop"/> already relies on the
    /// active device staying sticky through a deliberate stop to block hijack;
    /// treating "no report since I stopped" as staleness would undo that on a
    /// timer.
    /// </summary>
    internal static bool IsActiveDeviceStale(MusicPlayerState state, DateTime nowUtc)
    {
        if (!state.PlayState || string.IsNullOrEmpty(state.DeviceId))
            return false;

        return nowUtc - state.LastActiveHeartbeatUtc
            > TimeSpan.FromMilliseconds(ActiveDeviceStaleTimeoutMs);
    }

    /// <summary>
    /// True when callerDeviceId is the device MusicPlayerState.DeviceId currently
    /// names as active. Used to make sure only the active device's own position
    /// reports can refresh <see cref="MusicPlayerState.LastActiveHeartbeatUtc"/> —
    /// a stray report from a passive client must never mask a truly-dead active
    /// device from <see cref="IsActiveDeviceStale"/>.
    /// </summary>
    internal static bool IsCallerTheActiveDevice(MusicPlayerState state, string? callerDeviceId)
    {
        return !string.IsNullOrEmpty(state.DeviceId)
            && !string.IsNullOrEmpty(callerDeviceId)
            && state.DeviceId.Equals(callerDeviceId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The active device stopped proving life while the session was still marked
    /// playing — a backgrounded/crashed player or an anonymous Cast Receiver that
    /// never cleanly disconnects. Ends the session outright (mirrors
    /// <see cref="MusicPlaybackCommandHandler.HandleStop"/>'s shape) rather than
    /// auto-promoting another connected-but-passive device: silently starting
    /// audio somewhere the user never asked for it would be a worse surprise than
    /// the bug this fixes. Clearing DeviceId — unlike a deliberate stop, which
    /// keeps it to block hijack — is the deliberate difference: the device itself
    /// is presumed gone, so the very next StartPlaybackCommand from any connected
    /// device must be free to become active immediately.
    /// </summary>
    internal async Task EndStaleActiveSessionAsync(User user, MusicPlayerState state)
    {
        string? staleDeviceId = state.DeviceId;

        _logger?.LogWarning(
            "Music session for user {UserId}: active device {DeviceId} stopped reporting position for over {TimeoutMs}ms — ending the session",
            user.Id,
            staleDeviceId,
            ActiveDeviceStaleTimeoutMs
        );

        RemoveTimer(user.Id);

        if (!string.IsNullOrEmpty(staleDeviceId))
            _activeDeviceRegistry.RemoveIfMatches(user.Id, staleDeviceId);

        state.CurrentItem = null;
        state.PlayState = false;
        state.Time = 0;
        state.Backlog = [];
        state.Playlist = [];
        state.CurrentList = new("", UriKind.Relative);
        state.DeviceId = null;
        state.Actions = new()
        {
            Disallows = new()
            {
                Previous = true,
                Next = true,
                Resuming = true,
                Pausing = true,
                Seeking = true,
                Stopping = true,
                Muting = true,
                TogglingShuffle = true,
                TogglingRepeatContext = true,
                TogglingRepeatTrack = true,
            },
        };

        await UpdatePlaybackState(user, state);
    }

    /// <summary>
    /// Called by the active client (via <see cref="NoMercy.Api.Hubs.MusicHub.CrossfadeStartCommand"/>)
    /// when it begins the crossfade volume ramp.  Suppresses server auto-advance for this user
    /// for <paramref name="fadeDurationMs"/> + <see cref="CrossfadeSafetyMarginMs"/> milliseconds.
    /// Only the device matching <paramref name="deviceId"/> may later complete or override the
    /// suppression; competing commands from other devices are ignored.
    /// </summary>
    public void StartCrossfade(Guid userId, string deviceId, int fadeDurationMs)
    {
        if (!_stateManager.TryGetValue(userId, out MusicPlayerState? state))
            return;

        state.IsCrossfading = true;
        state.CrossfadeDeviceId = deviceId;
        state.CrossfadeTimeout = DateTime.UtcNow.AddMilliseconds(
            fadeDurationMs + CrossfadeSafetyMarginMs
        );
    }

    /// <summary>
    /// Called by the active client (via <see cref="NoMercy.Api.Hubs.MusicHub.CrossfadeCompleteCommand"/>)
    /// once the crossfade has finished and the new track is fully playing.  Sets the server state
    /// to the new track, resets time to zero, clears the crossfade suppression, and broadcasts
    /// the updated state to all connected clients.
    /// </summary>
    public async Task CompleteCrossfade(User user, string deviceId, Guid newTrackId)
    {
        if (!_stateManager.TryGetValue(user.Id, out MusicPlayerState? state))
            return;

        // Only accept from the device that started the crossfade.
        if (state.CrossfadeDeviceId != deviceId)
            return;

        // Locate the new track.  It must be at the head of the upcoming playlist.
        PlaylistTrackDto? newTrack = state.Playlist.FirstOrDefault(t => t.Id == newTrackId);
        if (newTrack is null)
        {
            // Track not found — clear the flag and let the server do its own advance on the
            // next timer tick (Time is already >= Duration at this point).
            state.IsCrossfading = false;
            state.CrossfadeDeviceId = null;
            return;
        }

        RemoveTimer(user.Id);

        int currentIndex = state.Playlist.IndexOf(newTrack);

        // Move the current item to the backlog.
        if (state.CurrentItem != null)
            state.Backlog.Add(state.CurrentItem);

        // Move all tracks before newTrack to the backlog (they were skipped).
        for (int i = 0; i < currentIndex; i++)
            state.Backlog.Add(state.Playlist[i]);

        state.Playlist.RemoveRange(0, currentIndex + 1);
        state.CurrentItem = newTrack;
        state.Time = 0;
        state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
        state.CrossfadeSignalSent = false;
        state.IsCrossfading = false;
        state.CrossfadeDeviceId = null;
        state.PlayState = true;

        await UpdatePlaybackState(user, state);
        StartPlaybackTimer(user);
    }

    public void DebouncedUpdatePlaybackState(User user, MusicPlayerState state)
    {
        if (_broadcastTimers.TryRemove(user.Id, out Timer? existing))
            existing.Dispose();

        Timer timer = new(
            async _ =>
            {
                _broadcastTimers.TryRemove(user.Id, out Timer? _);
                await UpdatePlaybackState(user, state);
            },
            null,
            BroadcastDebounceMs,
            Timeout.Infinite
        );

        _broadcastTimers[user.Id] = timer;
    }

    private async Task HandleTrackCompletion(User user, MusicPlayerState state)
    {
        if (state.CurrentItem == null)
            return;
        RemoveTimer(user.Id);

        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);

        bool wasLastTrack = state.Repeat == "off" && currentIndex + 1 >= state.Playlist.Count;
        if (wasLastTrack)
        {
            await PublishCompletedEventAsync(user.Id, state);
        }

        UpdateStateBasedOnRepeatMode(state, currentIndex);
        state.CrossfadeSignalSent = false; // Reset for next track

        await UpdatePlaybackState(user, state);
        StartPlaybackTimer(user);
    }

    public async Task ApplyItemLikeAsync(
        Guid userId,
        Guid itemId,
        bool liked,
        CancellationToken cancellationToken = default
    )
    {
        if (!_stateManager.TryGetValue(userId, out MusicPlayerState? playerState))
            return;

        if (playerState.CurrentItem is not null && playerState.CurrentItem.Id == itemId)
            playerState.CurrentItem.Favorite = liked;

        foreach (PlaylistTrackDto track in playerState.Playlist)
            if (track.Id == itemId)
                track.Favorite = liked;

        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? user = await userRepository.GetByIdAsync(userId);
        if (user is null)
            return;

        await UpdatePlaybackState(user, playerState);
    }

    public async Task UpdatePlaybackState(User user, MusicPlayerState? state)
    {
        if (state is not null)
            state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        EventPayload<PlayerStateEventElement> payload = new()
        {
            Events =
            [
                new()
                {
                    Event = new() { EventId = PlayerStateEventId, State = state },
                    Source = "musicHub",
                    Type = MusicEventType.PlayerStateChanged,
                    User = user,
                },
            ],
        };

        await _clientMessenger.SendTo("MusicPlayerState", "musicHub", user.Id, payload);
    }

    internal async Task PublishStartedEventAsync(Guid userId, MusicPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = state.CurrentItem.Id.ToString(),
                MediaType = "music",
                DeviceId = state.DeviceId,
            }
        );
    }

    private async Task PublishProgressEventAsync(Guid userId, MusicPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        int duration = state.CurrentItem.Duration.ToMilliSeconds();

        await bus.PublishAsync(
            new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = state.CurrentItem.Id.ToString(),
                Position = TimeSpan.FromMilliseconds(state.Time),
                Duration = TimeSpan.FromMilliseconds(duration),
            }
        );
    }

    private async Task PublishCompletedEventAsync(Guid userId, MusicPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = state.CurrentItem.Id.ToString(),
                MediaType = "music",
            }
        );
    }

    private static PlaylistTrackDto? PeekNextTrack(MusicPlayerState state)
    {
        if (state.Repeat == "one")
            return null; // Same track restarts, no crossfade

        if (state.Playlist.Count > 0)
            return state.Playlist.First();

        if (state.Repeat == "all" && state.Backlog.Count > 0)
            return state.Backlog.First(); // Will loop around

        return null; // No next track (playback will stop)
    }

    private async Task SendPrepareCrossfadeSignal(User user, PlaylistTrackDto nextItem)
    {
        var payload = new
        {
            next_item = nextItem,
            crossfade_duration_ms = 3000,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        await _clientMessenger.SendTo("PrepareCrossfade", "musicHub", user.Id, payload);
    }

    private void UpdateStateBasedOnRepeatMode(MusicPlayerState state, int currentIndex)
    {
        switch (state.Repeat)
        {
            case "one":
                state.Time = 0;
                break;
            case "all":
                if (currentIndex == state.Playlist.Count - 1)
                {
                    // Move the current item to the backlog
                    if (state.CurrentItem != null)
                        state.Backlog.Add(state.CurrentItem);

                    // Move the backlog to the playlist and start from the beginning
                    state.Playlist = [.. state.Backlog];
                    state.Backlog.Clear();

                    if (state.Playlist.Count > 0)
                    {
                        state.CurrentItem = state.Playlist.First();
                        state.Playlist.RemoveAt(0);
                        state.Time = 0;
                        state.PlayState = true;
                    }
                    else
                    {
                        // If the playlist is empty, stop playback
                        state.PlayState = false;
                        state.Time = 0;
                        state.CurrentItem = null;
                    }
                }
                else
                {
                    if (state.CurrentItem != null)
                        state.Backlog.Add(state.CurrentItem);
                    state.CurrentItem = state.Playlist[currentIndex + 1];
                    state.Playlist.RemoveAt(currentIndex + 1);
                    state.Time = 0;
                }

                break;
            default:
                if (state.CurrentItem != null)
                    state.Backlog.Add(state.CurrentItem);
                if (currentIndex + 1 < state.Playlist.Count)
                {
                    state.PlayState = true;
                    state.Time = 0;
                    state.CurrentItem = state.Playlist[currentIndex + 1];
                    state.Playlist.RemoveAt(currentIndex + 1);
                }
                else
                {
                    state.PlayState = false;
                    state.Time = 0;
                    state.CurrentItem = null;
                }

                break;
        }
    }
}
