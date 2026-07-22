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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Services.Music;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Discovery;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Configuration;
using NoMercy.Setup.Cast;

namespace NoMercy.Api.Hubs;

public partial class MusicHub : ConnectionHub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientMessenger _clientMessenger;
    private readonly MusicPlaybackService _musicPlaybackService;
    private readonly MusicPlayerStateManager _musicPlayerStateManager;
    private readonly MusicDeviceManager _musicDeviceManager;
    private readonly MusicPlaylistManager _musicPlaylistManager;
    private readonly MusicPlaybackCommandHandler _commandHandler;
    private readonly DeviceBusRegistry _busRegistry;
    private readonly CastSessionTokenService _castTokenService;
    private readonly MusicActiveDeviceRegistry _activeDeviceRegistry;
    private readonly INetworkDiscovery? _networkDiscovery;

    private readonly IChromeCastService _chromeCast;
    private readonly CastPanelWakeLauncher _castPanelWakeLauncher;

    private readonly ILogger<MusicHub> _logger;

    public MusicHub(
        ILogger<MusicHub> logger,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger,
        MusicPlaybackService musicPlaybackService,
        MusicPlayerStateManager musicPlayerStateManager,
        MusicDeviceManager musicDeviceManager,
        MusicPlaylistManager musicPlaylistManager,
        MusicPlaybackCommandHandler commandHandler,
        IActivityLogger activityLogger,
        DeviceBusRegistry busRegistry,
        CastSessionTokenService castTokenService,
        IChromeCastService chromeCast,
        CastPanelWakeLauncher castPanelWakeLauncher,
        MusicActiveDeviceRegistry activeDeviceRegistry,
        INetworkDiscovery? networkDiscovery = null
    )
        : base(httpContextAccessor: httpContextAccessor, contextFactory: contextFactory, connectedClients: connectedClients, activityLogger: activityLogger)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _clientMessenger = clientMessenger;
        _musicPlaybackService = musicPlaybackService;
        _musicPlayerStateManager = musicPlayerStateManager;
        _musicDeviceManager = musicDeviceManager;
        _musicPlaylistManager = musicPlaylistManager;
        _commandHandler = commandHandler;
        _busRegistry = busRegistry;
        _castTokenService = castTokenService;
        _chromeCast = chromeCast;
        _castPanelWakeLauncher = castPanelWakeLauncher;
        _activeDeviceRegistry = activeDeviceRegistry;
        _networkDiscovery = networkDiscovery;
    }

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CommandLocks = new();

    private static SemaphoreSlim GetUserLock(Guid userId)
    {
        return CommandLocks.GetOrAdd(key: userId, valueFactory: _ => new(initialCount: 1, maxCount: 1));
    }

    // Rebuilds the per-device volume map carried on every broadcast so a
    // controller can render a slider per device and each device can read its
    // own level. Scoped to the caller's user so one user never sees another's
    // devices. Never-set volumes coalesce to the same safe default the scoped
    // volume_percentage field uses.

    // Back-compat: position in whole seconds. Quantizes to 1000ms, which is a
    // dominant source of cross-device drift. New clients call ReportPositionCommand.

    // The active device reports its real audio position in MILLISECONDS. This is
    // the playback truth; the server relays it so every passive client computes
    // the same position via reference-time (position + (serverNow - timestamp)).

    // Clock-sync handshake. A client samples this a few times, keeps the
    // lowest-RTT result, and derives offset = serverTime + rtt/2 - clientRecv so
    // it can convert its local clock to the shared server clock. Every device
    // using the same offset-corrected clock computes the same playback position
    // regardless of its own wall-clock skew.

    // Back-compat entry point: targets the active device (null deviceId).
    // Old clients invoke this with a single argument; SignalR is strict about
    // argument counts, so the signature must stay intact.

    // Sets the volume of a NAMED device (null deviceId = the active device).
    // Volume is owned by the device: setting a passive device's volume updates
    // that device's stored level and the broadcast device_volumes map without
    // disturbing the active device's playback level.

    // Resolves which device a volume command targets, scoped to the requesting
    // user so one user can never address another's device. Null/empty deviceId
    // falls back to the user's active device (back-compat with ChangeVolumeCommand).

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;

        // Wait briefly so the client's 'ConnectedDevicesState' handler is
        // registered before the broadcast lands. If the connection drops
        // during this window, bail out — sending state to a dead connection
        // just throws inside the messenger.
        try
        {
            await Task.Delay(millisecondsDelay: 500, cancellationToken: Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Send updated device list to all connected devices for this user
        List<Device> connectedDevices = await MusicDevicesAsync();
        await _clientMessenger.SendTo(
            name: "ConnectedDevicesState",
            endpoint: "musicHub",
            userId: user.Id,
            data: connectedDevices
        );

        if (_musicPlayerStateManager.TryGetValue(userId: user.Id, state: out MusicPlayerState? playerState))
        {
            UpdateActionsDisallows(state: playerState);

            // A newly-connected device reports its own current volume via the
            // client_volume query param; seed/refresh device_volumes now so a
            // controller opened on another device sees this device's slider
            // immediately, without waiting for a playlist change or volume set.
            UpdateDeviceVolumes(state: playerState, userSub: user.Id);

            await _musicPlaybackService.UpdatePlaybackState(user: user, state: playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user: user, state: new());
        }

        _logger.LogDebug(message: "Music client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user == null)
            return;

        bool stopPlayback = false;
        bool wasCurrentDevice = false;
        Ulid stoppedDeviceId = Ulid.Empty;
        Guid stoppedTrackId = Guid.Empty;
        string? stoppedTitle = null;
        string? stoppedClientDeviceId = null;

        if (ConnectedClients.Clients.TryGetValue(key: Context.ConnectionId, value: out Client? client))
            if (_musicPlayerStateManager.TryGetValue(userId: user.Id, state: out MusicPlayerState? state))
                if (state.DeviceId == client.DeviceId)
                {
                    // One device_id can hold several hub connections (e.g. the KMP
                    // double-connect). Only treat this as the active device actually
                    // leaving when no OTHER musicHub connection for the same device_id
                    // survives this disconnect — otherwise tearing down one of two live
                    // connections would wrongly release a device that is still very much
                    // connected on its other socket.
                    bool otherConnectionForDeviceSurvives = ConnectedClients.Clients.Any(predicate: kvp =>
                        kvp.Key != Context.ConnectionId
                        && kvp.Value.DeviceId.Equals(
                            value: client.DeviceId,
                            comparisonType: StringComparison.OrdinalIgnoreCase
                        )
                        && kvp.Value.Endpoint.Contains(
                            value: "musicHub",
                            comparisonType: StringComparison.OrdinalIgnoreCase
                        )
                    );

                    if (!otherConnectionForDeviceSurvives)
                    {
                        _musicPlaybackService.RemoveTimer(userId: user.Id);

                        _musicDeviceManager.RemoveUserDevice(userId: user.Id);

                        stopPlayback = true;
                        wasCurrentDevice = true;
                        stoppedDeviceId = client.Id;
                        stoppedTrackId = state.CurrentItem?.Id ?? Guid.Empty;
                        stoppedTitle = state.CurrentItem?.Name;
                        stoppedClientDeviceId = client.DeviceId;
                    }
                }

        await base.OnDisconnectedAsync(exception: exception);

        if (_musicPlayerStateManager.TryGetValue(userId: user.Id, state: out MusicPlayerState? playerState))
        {
            List<Device> connectedDevices = await MusicDevicesAsync();

            // Send updated device list to all remaining connected devices
            await _clientMessenger.SendTo(
                name: "ConnectedDevicesState",
                endpoint: "musicHub",
                userId: user.Id,
                data: connectedDevices
            );

            if (connectedDevices.Count == 0)
            {
                _activeDeviceRegistry.Remove(userId: user.Id);

                // Clean up CommandLock and player state — no connections remain for this user
                if (CommandLocks.TryRemove(key: user.Id, value: out SemaphoreSlim? removedLock))
                    removedLock.Dispose();

                _musicPlayerStateManager.RemoveState(userId: user.Id);
                playerState = null;
            }
            else if (stopPlayback)
            {
                // Remove current device if it was the disconnecting device
                if (wasCurrentDevice && !string.IsNullOrEmpty(value: stoppedClientDeviceId))
                {
                    _activeDeviceRegistry.RemoveIfMatches(userId: user.Id, deviceId: stoppedClientDeviceId);
                }

                // Graceful release regardless of whether the vanished device was mid-play
                // or already paused: this branch only runs when connectedDevices.Count > 0
                // (the connectedDevices.Count == 0 branch above owns genuine session-end),
                // so another device is still around and may want to resume the exact spot
                // this one left off. Ending the session outright here — as a prior revision
                // did for the "already paused" case — wiped CurrentItem/Backlog/Playlist on
                // every remaining device the instant a paused active device disconnected,
                // which the KMP client renders as a hard stop (MusicHubAdapter.stop() on a
                // null item), not a pause. The session survives so a reconnect or another
                // device can claim it, but nobody owns it anymore. Clearing DeviceId here —
                // not just the MusicActiveDeviceRegistry entry above — is what lets the very
                // next command from ANY connected device win active; leaving it set is
                // exactly the divergence MusicActiveDeviceRegistry's own doc comment warns
                // about (the registry says "no active device" while MusicPlayerState.DeviceId
                // still names the device that just vanished), which is what wedged every
                // other device's command into a void during the live incident this fixes.
                playerState.PlayState = false;
                playerState.DeviceId = null;
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Pausing = true,
                        Resuming = false,
                        Previous =
                            playerState.CurrentItem == null || playerState.Backlog.Count <= 1,
                        Next =
                            playerState.CurrentItem == null
                            || (
                                playerState.Playlist.IndexOf(item: playerState.CurrentItem)
                                    >= playerState.Playlist.Count - 1
                                && playerState.Repeat == "off"
                            ),
                        Seeking = false,
                        Stopping = false,
                        Muting = false,
                        TogglingShuffle = false,
                        TogglingRepeatContext = false,
                        TogglingRepeatTrack = false,
                    },
                };
            }
        }

        await _musicPlaybackService.UpdatePlaybackState(user: user, state: playerState);

        if (stopPlayback && stoppedDeviceId != Ulid.Empty)
        {
            try
            {
                await ActivityLogger.LogPlaybackAsync(
                    type: "playback.stopped",
                    userId: user.Id,
                    deviceId: stoppedDeviceId,
                    mediaId: Ulid.Empty,
                    metadata: new
                    {
                        media_type = "audio",
                        track_id = stoppedTrackId,
                        title = stoppedTitle,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(message: "Failed to log playback.stopped: {Message}", args: ex.Message);
            }
        }

        _logger.LogDebug(message: "Music client disconnected");
    }

    // ── Cast-receiver helpers (Phase 0) ──────────────────────────────────────

    private string ResolveServerUrl()
    {
        // Public origin the receiver should use for API + SignalR. NetworkDiscovery
        // owns the authoritative external URL once Connectivity has resolved a path
        // (Cloudflare tunnel, port-forward, or STUN). Fall back to ApiBaseUrl in
        // the rare case Discovery isn't ready yet — receiver will get a working
        // URL on the next launch once Connectivity stabilizes.
        string? external = _networkDiscovery?.ExternalAddress;
        return string.IsNullOrEmpty(value: external)
            ? ExternalServicesConfig.Current.ApiBaseUrl
            : external;
    }

    private string ResolveSenderLocale()
    {
        // Sender ships its locale via standard Accept-Language. We pick the first
        // tag and pass it through; receiver uses it to seed i18n on first paint.
        string? header =
            _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrEmpty(value: header))
            return "en-US";

        string first = header.Split(separator: ',')[0].Split(separator: ';')[0].Trim();
        return string.IsNullOrEmpty(value: first) ? "en-US" : first;
    }

    private CastIntent ResolveMusicIntent(Guid userId, string targetDeviceId)
    {
        // If the user has a live music player state when handing off to the TV,
        // the receiver should resume that exact list. Otherwise idle — receiver
        // shows the splash and waits for user input or a follow-up command.
        if (!_musicPlayerStateManager.TryGetValue(userId: userId, state: out MusicPlayerState? state))
            return CastIntent.Idle();
        if (state.CurrentItem is null || state.CurrentList is null)
            return CastIntent.Idle();

        // CurrentList is "/music/{type}/{listId}" — split it back out.
        string path = state.CurrentList.ToString().TrimStart(trimChar: '/');
        string[] parts = path.Split(separator: '/');
        if (
            parts.Length < 3
            || !string.Equals(a: parts[0], b: "music", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
            return CastIntent.Idle();

        string listType = MusicPlayerStateFactory.FromRouteSegment(segment: parts[1]);
        string listId = parts[2];
        string trackId = state.CurrentItem.Id.ToString();
        int? resumeAt = state.Time > 0 ? state.Time / 1000 : null;
        return CastIntent.PlayMusic(listType: listType, listId: listId, trackId: trackId, resumeAt: resumeAt);
    }
}
