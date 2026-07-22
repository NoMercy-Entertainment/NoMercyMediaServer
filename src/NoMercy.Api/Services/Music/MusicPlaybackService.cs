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
    private static int PlayerStateEventId => Interlocked.Increment(location: ref field);

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
    private readonly ConcurrentDictionary<Guid, byte> _playbackStartsInFlight = new();

    // Fingerprint of the last MusicPlayerState broadcast actually sent per user —
    // see ShouldCoalesceBroadcast, which drops a redundant repeat of it.
    private readonly ConcurrentDictionary<Guid, BroadcastFingerprint> _lastBroadcastFingerprints =
        new();
    private const int TimerInterval = 100;
    private const int CrossfadeLeewayMs = 10000; // Send PrepareCrossfade 10s before track end (gives time to buffer)
    private const int BroadcastDebounceMs = 150;
    private const int MinPlayTimeForRecordMs = 10_000;

    // Window within which two broadcasts for the same user, same track, same
    // play/pause state, and the same second-granularity position are treated
    // as the redundant echo of one real state change rather than two distinct
    // ones — collapsing the bursts the many ungated UpdatePlaybackState call
    // sites (StartPlayback, HandleTrackCompletion, ApplyItemLikeAsync, command
    // handlers, ...) can produce within milliseconds of each other. This is a
    // volume reduction only: out-of-order delivery is corrected by
    // MusicPlayerState.Seq, not by this window, so it stays short and
    // conservative — when unsure, UpdatePlaybackState emits.
    private const int BroadcastCoalesceWindowMs = 50;
    private const int BroadcastTimeBucketMs = 1000;

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
        return _stateLocks.GetOrAdd(key: userId, valueFactory: _ => new(initialCount: 1, maxCount: 1));
    }

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(key: user.Id, value: out Timer? existingTimer))
            existingTimer.Dispose();

        if (!_stateManager.TryGetValue(userId: user.Id, state: out MusicPlayerState? playerState))
            return;

        // Any (re)start of the ticking loop counts as proof of life, regardless of
        // who asked for it. Playback can be legitimately resumed remotely from any
        // connected device (not just the active one), so gating this on caller
        // identity would either reject a valid remote resume or reintroduce the
        // exact bug this exists to prevent: resuming after a long pause must not
        // make the active device look instantly stale to the check below.
        playerState.LastActiveHeartbeatUtc = DateTime.UtcNow;

        SemaphoreSlim stateLock = GetStateLock(userId: user.Id);

        Timer timer = new(
            callback: async _ =>
            {
                if (!await stateLock.WaitAsync(millisecondsTimeout: 0))
                    return; // Skip tick if previous tick is still running
                try
                {
                    if (!_stateManager.TryGetValue(userId: user.Id, state: out MusicPlayerState? playerState))
                        return;
                    if (!playerState.PlayState || playerState.CurrentItem is null)
                        return;

                    if (
                        IsActiveDeviceStale(state: playerState, nowUtc: DateTime.UtcNow)
                        && !IsPlaybackStartInFlight(userId: user.Id)
                    )
                    {
                        await EndStaleActiveSessionAsync(user: user, state: playerState);
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
                        await ctx.MusicPlays.AddAsync(entity: new(userId: user.Id, trackId: playerState.CurrentItem.Id));
                        await ctx.SaveChangesAsync();
                        await PublishProgressEventAsync(userId: user.Id, state: playerState);
                    }

                    // Send crossfade signal to clients ~10s before track ends
                    if (
                        playerState.Time >= duration - CrossfadeLeewayMs
                        && !playerState.CrossfadeSignalSent
                    )
                    {
                        playerState.CrossfadeSignalSent = true;
                        PlaylistTrackDto? nextItem = PeekNextTrack(state: playerState);
                        if (nextItem != null)
                            await SendPrepareCrossfadeSignal(user: user, nextItem: nextItem);
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

                        await HandleTrackCompletion(user: user, state: playerState);
                    }
                }
                finally
                {
                    stateLock.Release();
                }
            },
            state: null,
            dueTime: 100,
            period: TimerInterval
        );

        _timers[key: user.Id] = timer;
    }

    public void RemoveTimer(Guid userId)
    {
        if (_timers.TryRemove(key: userId, value: out Timer? timer))
            timer.Dispose();
    }

    /// <summary>
    /// Marks a <see cref="NoMercy.Api.Hubs.MusicHub.StartPlaybackCommand"/> as in flight for
    /// <paramref name="userId"/> — see <see cref="IsPlaybackStartInFlight"/> for why the
    /// staleness watchdog needs to know this. Also stamps a fresh heartbeat immediately if a
    /// session already exists, covering a slow playlist fetch on an EXISTING session: the old
    /// timer from before this call keeps ticking through the whole fetch, and without this
    /// stamp it would only see the stale pre-fetch heartbeat until the fetch finishes.
    /// Call from a try/finally around the whole command body, paired with
    /// <see cref="EndPlaybackStart"/>.
    /// </summary>
    internal void BeginPlaybackStart(Guid userId)
    {
        _playbackStartsInFlight[key: userId] = 0;

        if (_stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? state))
            state.LastActiveHeartbeatUtc = DateTime.UtcNow;
    }

    internal void EndPlaybackStart(Guid userId)
    {
        _playbackStartsInFlight.TryRemove(key: userId, value: out _);
    }

    /// <summary>
    /// True while a <see cref="NoMercy.Api.Hubs.MusicHub.StartPlaybackCommand"/> is in flight for
    /// <paramref name="userId"/>. SignalR dispatches invocations on one connection serially by
    /// default, so a slow playlist fetch (contended DB, cold cache, a large artist) starves that
    /// same connection's queued position reports for its entire duration — the active device is
    /// very much alive, just queued behind its own start command. The 100ms watchdog tick
    /// consults this before killing a session on staleness so it can't race a legitimate,
    /// still-in-progress start; once the flag clears, a genuinely dead device is still caught on
    /// the very next tick against whatever heartbeat <see cref="BeginPlaybackStart"/> or a real
    /// report last stamped.
    /// </summary>
    internal bool IsPlaybackStartInFlight(Guid userId)
    {
        return _playbackStartsInFlight.ContainsKey(key: userId);
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
        if (!state.PlayState || string.IsNullOrEmpty(value: state.DeviceId))
            return false;

        return nowUtc - state.LastActiveHeartbeatUtc
            > TimeSpan.FromMilliseconds(milliseconds: ActiveDeviceStaleTimeoutMs);
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
        return !string.IsNullOrEmpty(value: state.DeviceId)
            && !string.IsNullOrEmpty(value: callerDeviceId)
            && state.DeviceId.Equals(value: callerDeviceId, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Single entry point every inbound hub call that can prove the active device is
    /// alive should route through — a position report, a playback command, or a cheap
    /// state read. Refreshes <see cref="MusicPlayerState.LastActiveHeartbeatUtc"/> and
    /// returns true only when <paramref name="callerDeviceId"/> is
    /// <see cref="IsCallerTheActiveDevice"/>; a passive caller is a complete no-op so it
    /// can never mask a truly-dead active device from <see cref="IsActiveDeviceStale"/>
    /// nor (via the caller's own use of the return value) overwrite the active device's
    /// authoritative position with its own.
    /// </summary>
    internal static bool TryRefreshHeartbeat(MusicPlayerState state, string? callerDeviceId)
    {
        if (!IsCallerTheActiveDevice(state: state, callerDeviceId: callerDeviceId))
            return false;

        state.LastActiveHeartbeatUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Gate for <see cref="NoMercy.Api.Hubs.MusicHub.ReportPositionForItemCommand"/>:
    /// a null <paramref name="itemId"/> is untagged and always passes (no stale
    /// check possible, matches the untagged commands' existing behavior); a
    /// non-null itemId must parse as a Guid and match
    /// <see cref="MusicPlayerState.CurrentItem"/>'s id (an unparsable id or no
    /// current item both count as a mismatch). Case-insensitive by virtue of
    /// comparing parsed Guids rather than raw strings.
    /// </summary>
    internal static bool IsReportForCurrentItem(MusicPlayerState state, string? itemId)
    {
        if (itemId is null)
            return true;

        return state.CurrentItem is not null
            && Guid.TryParse(input: itemId, result: out Guid parsed)
            && parsed == state.CurrentItem.Id;
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
            message: "Music session for user {UserId}: active device {DeviceId} stopped reporting position for over {TimeoutMs}ms — ending the session", args: [user.Id, staleDeviceId, ActiveDeviceStaleTimeoutMs]
        );

        RemoveTimer(userId: user.Id);

        if (!string.IsNullOrEmpty(value: staleDeviceId))
            _activeDeviceRegistry.RemoveIfMatches(userId: user.Id, deviceId: staleDeviceId);

        state.CurrentItem = null;
        state.PlayState = false;
        state.SetPosition(positionMs: 0);
        state.Backlog = [];
        state.Playlist = [];
        state.CurrentList = new(uriString: "", uriKind: UriKind.Relative);
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

        await UpdatePlaybackState(user: user, state: state);
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
        if (!_stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? state))
            return;

        state.IsCrossfading = true;
        state.CrossfadeDeviceId = deviceId;
        state.CrossfadeTimeout = DateTime.UtcNow.AddMilliseconds(
            value: fadeDurationMs + CrossfadeSafetyMarginMs
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
        if (!_stateManager.TryGetValue(userId: user.Id, state: out MusicPlayerState? state))
            return;

        // Only accept from the device that started the crossfade.
        if (state.CrossfadeDeviceId != deviceId)
            return;

        // Locate the new track.  It must be at the head of the upcoming playlist.
        PlaylistTrackDto? newTrack = state.Playlist.FirstOrDefault(predicate: t => t.Id == newTrackId);
        if (newTrack is null)
        {
            // Track not found — clear the flag and let the server do its own advance on the
            // next timer tick (Time is already >= Duration at this point).
            state.IsCrossfading = false;
            state.CrossfadeDeviceId = null;
            return;
        }

        RemoveTimer(userId: user.Id);

        int currentIndex = state.Playlist.IndexOf(item: newTrack);

        // Move the current item to the backlog.
        if (state.CurrentItem != null)
            state.Backlog.Add(item: state.CurrentItem);

        // Move all tracks before newTrack to the backlog (they were skipped).
        for (int i = 0; i < currentIndex; i++)
            state.Backlog.Add(item: state.Playlist[index: i]);

        state.Playlist.RemoveRange(index: 0, count: currentIndex + 1);
        state.CurrentItem = newTrack;
        state.SetPosition(positionMs: 0);
        state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(value: 1);
        state.CrossfadeSignalSent = false;
        state.IsCrossfading = false;
        state.CrossfadeDeviceId = null;
        state.PlayState = true;

        await UpdatePlaybackState(user: user, state: state);
        StartPlaybackTimer(user: user);
    }

    public void DebouncedUpdatePlaybackState(User user, MusicPlayerState state)
    {
        if (_broadcastTimers.TryRemove(key: user.Id, value: out Timer? existing))
            existing.Dispose();

        // Epoch-ms so this line correlates directly against the scheduling call's
        // own "broadcastMs" mark in MusicHub.PlaybackCommand — that mark is only
        // when the Timer was ARMED; this is when the relay actually fired and sent.
        long scheduledMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Timer timer = new(
            callback: async _ =>
            {
                _broadcastTimers.TryRemove(key: user.Id, value: out Timer? _);
                long firedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await UpdatePlaybackState(user: user, state: state);
                long sentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger?.LogInformation(
                    message: "{UserId}: [MusicPlaybackService.DebouncedUpdatePlaybackState] scheduledMs={ScheduledMs} firedMs={FiredMs} (+{FireDelayMs}ms) sentMs={SentMs} (+{SendMs}ms) totalMs={TotalMs}ms playlist={PlaylistCount} backlog={BacklogCount}", args: [user.Id, scheduledMs, firedMs, firedMs - scheduledMs, sentMs, sentMs - firedMs, sentMs - scheduledMs, state.Playlist.Count, state.Backlog.Count]
                );
            },
            state: null,
            dueTime: BroadcastDebounceMs,
            period: Timeout.Infinite
        );

        _broadcastTimers[key: user.Id] = timer;
    }

    private async Task HandleTrackCompletion(User user, MusicPlayerState state)
    {
        if (state.CurrentItem == null)
            return;
        RemoveTimer(userId: user.Id);

        int currentIndex = state.Playlist.IndexOf(item: state.CurrentItem);

        bool wasLastTrack = state.Repeat == "off" && currentIndex + 1 >= state.Playlist.Count;
        if (wasLastTrack)
        {
            await PublishCompletedEventAsync(userId: user.Id, state: state);
        }

        UpdateStateBasedOnRepeatMode(state: state, currentIndex: currentIndex);
        state.CrossfadeSignalSent = false; // Reset for next track

        await UpdatePlaybackState(user: user, state: state);
        StartPlaybackTimer(user: user);
    }

    public async Task ApplyItemLikeAsync(
        Guid userId,
        Guid itemId,
        bool liked,
        CancellationToken cancellationToken = default
    )
    {
        if (!_stateManager.TryGetValue(userId: userId, state: out MusicPlayerState? playerState))
            return;

        if (playerState.CurrentItem is not null && playerState.CurrentItem.Id == itemId)
            playerState.CurrentItem.Favorite = liked;

        foreach (PlaylistTrackDto track in playerState.Playlist)
            if (track.Id == itemId)
                track.Favorite = liked;

        await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? user = await userRepository.GetByIdAsync(userId: userId);
        if (user is null)
            return;

        await UpdatePlaybackState(user: user, state: playerState);
    }

    public async Task UpdatePlaybackState(User user, MusicPlayerState? state)
    {
        MusicPlayerState? broadcastState = null;
        if (state is not null)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (ShouldCoalesceBroadcast(userId: user.Id, state: state, nowMs: nowMs))
                return;

            // Same instant for both: Timestamp predates ServerTimeMs and some
            // clients may already key off it, so it stays; ServerTimeMs is the
            // new clock-sync reference every broadcast emit carries.
            state.Timestamp = nowMs;
            state.ServerTimeMs = nowMs;

            // Per-user monotonic, clock-anchored sequence stamped fresh on
            // every emit that reaches here — see MusicPlayerStateManager.NextSeq
            // and MusicPlayerState.Seq. Lets a client drop a broadcast that
            // arrives out of order instead of racing it against one it already
            // applied.
            state.Seq = _stateManager.NextSeq(userId: user.Id);

            // Broadcast a lyric-stripped queue projection, never the stored
            // state — see MusicPlayerState.CloneForBroadcast for why queue-item
            // lyrics are dead weight on this ~per-5s broadcast.
            broadcastState = state.CloneForBroadcast();
        }

        EventPayload<PlayerStateEventElement> payload = new()
        {
            Events =
            [
                new()
                {
                    Event = new() { EventId = PlayerStateEventId, State = broadcastState },
                    Source = "musicHub",
                    Type = MusicEventType.PlayerStateChanged,
                    User = user,
                },
            ],
        };

        await _clientMessenger.SendTo(name: "MusicPlayerState", endpoint: "musicHub", userId: user.Id, data: payload);
    }

    /// <summary>
    /// Drops a MusicPlayerState broadcast that is the redundant echo of the one
    /// immediately before it for this user: same track, same play/pause state,
    /// same second-granularity position, within
    /// <see cref="BroadcastCoalesceWindowMs"/> of the previous emit. Only
    /// volume is reduced here — out-of-order correctness is carried entirely by
    /// <see cref="MusicPlayerState.Seq"/>, so this stays conservative and never
    /// coalesces across a real change or a wider time gap.
    /// </summary>
    private bool ShouldCoalesceBroadcast(Guid userId, MusicPlayerState state, long nowMs)
    {
        BroadcastFingerprint fingerprint = new(
            ItemId: state.CurrentItem?.Id,
            PlayState: state.PlayState,
            TimeBucket: state.Time / BroadcastTimeBucketMs,
            AtMs: nowMs
        );

        if (
            _lastBroadcastFingerprints.TryGetValue(
                key: userId,
                value: out BroadcastFingerprint previousFingerprint
            )
            && previousFingerprint.ItemId == fingerprint.ItemId
            && previousFingerprint.PlayState == fingerprint.PlayState
            && previousFingerprint.TimeBucket == fingerprint.TimeBucket
            && nowMs - previousFingerprint.AtMs < BroadcastCoalesceWindowMs
        )
            return true;

        _lastBroadcastFingerprints[key: userId] = fingerprint;
        return false;
    }

    private readonly record struct BroadcastFingerprint(
        Guid? ItemId,
        bool PlayState,
        int TimeBucket,
        long AtMs
    );

    internal async Task PublishStartedEventAsync(Guid userId, MusicPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
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
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 0,
                MediaIdentifier = state.CurrentItem.Id.ToString(),
                Position = TimeSpan.FromMilliseconds(milliseconds: state.Time),
                Duration = TimeSpan.FromMilliseconds(milliseconds: duration),
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
            @event: new PlaybackCompletedEvent
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

        if (state is { Repeat: "all", Backlog.Count: > 0 })
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
        await _clientMessenger.SendTo(name: "PrepareCrossfade", endpoint: "musicHub", userId: user.Id, data: payload);
    }

    private void UpdateStateBasedOnRepeatMode(MusicPlayerState state, int currentIndex)
    {
        switch (state.Repeat)
        {
            case "one":
                state.SetPosition(positionMs: 0);
                break;
            case "all":
                if (currentIndex == state.Playlist.Count - 1)
                {
                    // Move the current item to the backlog
                    if (state.CurrentItem != null)
                        state.Backlog.Add(item: state.CurrentItem);

                    // Move the backlog to the playlist and start from the beginning
                    state.Playlist = [.. state.Backlog];
                    state.Backlog.Clear();

                    if (state.Playlist.Count > 0)
                    {
                        state.CurrentItem = state.Playlist.First();
                        state.Playlist.RemoveAt(index: 0);
                        state.SetPosition(positionMs: 0);
                        state.PlayState = true;
                    }
                    else
                    {
                        // If the playlist is empty, stop playback
                        state.PlayState = false;
                        state.SetPosition(positionMs: 0);
                        state.CurrentItem = null;
                    }
                }
                else
                {
                    if (state.CurrentItem != null)
                        state.Backlog.Add(item: state.CurrentItem);
                    state.CurrentItem = state.Playlist[index: currentIndex + 1];
                    state.Playlist.RemoveAt(index: currentIndex + 1);
                    state.SetPosition(positionMs: 0);
                }

                break;
            default:
                if (state.CurrentItem != null)
                    state.Backlog.Add(item: state.CurrentItem);
                if (currentIndex + 1 < state.Playlist.Count)
                {
                    state.PlayState = true;
                    state.SetPosition(positionMs: 0);
                    state.CurrentItem = state.Playlist[index: currentIndex + 1];
                    state.Playlist.RemoveAt(index: currentIndex + 1);
                }
                else
                {
                    state.PlayState = false;
                    state.SetPosition(positionMs: 0);
                    state.CurrentItem = null;
                }

                break;
        }
    }
}
