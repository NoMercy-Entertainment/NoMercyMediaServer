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
using System.Security.Claims;
using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
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
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Cast;
using Serilog.Events;

namespace NoMercy.Api.Hubs;

public partial class VideoHub : ConnectionHub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientMessenger _clientMessenger;
    private readonly VideoPlaybackService _videoPlaybackService;
    private readonly VideoPlayerStateManager _videoPlayerStateManager;
    private readonly VideoDeviceManager _videoDeviceManager;
    private readonly VideoPlaylistManager _videoPlaylistManager;
    private readonly VideoPlaybackCommandHandler _commandHandler;
    private readonly CastSessionTokenService _castTokenService;
    private readonly DeviceBusRegistry _busRegistry;
    private readonly INetworkDiscovery? _networkDiscovery;

    private readonly IDbContextFactory<MediaContext> _contextFactory;

    private readonly IChromeCastService _chromeCast;

    public VideoHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger,
        VideoPlaybackService videoPlaybackService,
        VideoPlayerStateManager videoPlayerStateManager,
        VideoDeviceManager videoDeviceManager,
        VideoPlaylistManager videoPlaylistManager,
        VideoPlaybackCommandHandler commandHandler,
        IActivityLogger activityLogger,
        CastSessionTokenService castTokenService,
        DeviceBusRegistry busRegistry,
        IChromeCastService chromeCast,
        INetworkDiscovery? networkDiscovery = null
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger)
    {
        _httpContextAccessor = httpContextAccessor;
        _clientMessenger = clientMessenger;
        _contextFactory = contextFactory;
        _videoPlaybackService = videoPlaybackService;
        _videoPlayerStateManager = videoPlayerStateManager;
        _videoDeviceManager = videoDeviceManager;
        _videoPlaylistManager = videoPlaylistManager;
        _commandHandler = commandHandler;
        _castTokenService = castTokenService;
        _busRegistry = busRegistry;
        _chromeCast = chromeCast;
        _networkDiscovery = networkDiscovery;
    }



    private static readonly ConcurrentDictionary<Guid, Device> CurrentDevice = new();













    // ── Cast-receiver helpers (Phase 0) ──────────────────────────────────────

    private string ResolveServerUrl()
    {
        string? external = _networkDiscovery?.ExternalAddress;
        return string.IsNullOrEmpty(external)
            ? ExternalServicesConfig.Current.ApiBaseUrl
            : external;
    }

    private string ResolveSenderLocale()
    {
        string? header =
            _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrEmpty(header))
            return "en-US";

        string first = header.Split(',')[0].Split(';')[0].Trim();
        return string.IsNullOrEmpty(first) ? "en-US" : first;
    }

    private CastIntent ResolveVideoIntent(Guid userId)
    {
        // If the user has a live video player state when handing off to the TV,
        // resume that exact item. Otherwise idle — receiver shows the splash.
        if (!_videoPlayerStateManager.TryGetValue(userId, out VideoPlayerState? state))
            return CastIntent.Idle();
        if (state.CurrentItem is null)
            return CastIntent.Idle();

        // CurrentList is "/{type}/{listId}/watch" — extract type for navigation.
        string path = state.CurrentList.ToString().TrimStart('/');
        string[] parts = path.Split('/');
        if (parts.Length < 2)
            return CastIntent.Idle();

        string mediaType = parts[0];
        string mediaId = state.CurrentItem.Id.ToString();
        int? resumeAt = state.Time > 0 ? state.Time / 1000 : null;
        return CastIntent.PlayVideo(mediaType, mediaId, resumeAt);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user == null)
            return;

        bool stopPlayback = false;
        Ulid stoppedDeviceId = Ulid.Empty;
        Ulid stoppedMediaId = Ulid.Empty;
        string? stoppedTitle = null;

        if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            if (_videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? state))
                if (state.DeviceId == client.DeviceId)
                {
                    _videoPlaybackService.RemoveTimer(user.Id);

                    _videoDeviceManager.RemoveUserDevice(user.Id);

                    stopPlayback = true;
                    stoppedDeviceId = client.Id;
                    stoppedMediaId = state.CurrentItem?.VideoId ?? Ulid.Empty;
                    stoppedTitle = state.CurrentItem?.Title;
                }

        await base.OnDisconnectedAsync(exception);

        if (_videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? playerState))
        {
            List<Device> connectedDevices = Devices();

            if (connectedDevices.Count == 0)
            {
                playerState.DeviceId = null;
                playerState.PlayState = false;
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Previous = true,
                        Next = true,
                        Resuming = true,
                        Pausing = true,
                        Muting = true,
                        Seeking = true,
                        Stopping = true,
                    },
                };
            }
            else if (stopPlayback)
            {
                playerState.PlayState = false;
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Pausing = !playerState.PlayState,
                        Resuming = playerState.PlayState,
                        Stopping = true,
                        Seeking = true,
                        Muting = true,
                        Previous =
                            playerState.CurrentItem is null
                            || playerState.Playlist.IndexOf(playerState.CurrentItem) == 0,
                        Next =
                            playerState.CurrentItem is null
                            || playerState.Playlist.IndexOf(playerState.CurrentItem)
                                == playerState.Playlist.Count - 1,
                    },
                };
            }
        }

        await _videoPlaybackService.UpdatePlaybackState(user, playerState);

        if (stopPlayback && stoppedDeviceId != Ulid.Empty)
        {
            try
            {
                await ActivityLogger.LogPlaybackAsync(
                    "playback.stopped",
                    user.Id,
                    stoppedDeviceId,
                    stoppedMediaId,
                    new { media_type = "video", title = stoppedTitle }
                );
            }
            catch (Exception ex)
            {
                Logger.Socket(
                    $"Failed to log playback.stopped: {ex.Message}",
                    LogEventLevel.Warning
                );
            }
        }

        Logger.Socket("Video client disconnected", LogEventLevel.Debug);
    }
}
