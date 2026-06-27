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
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;
using NoMercy.Networking;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Discovery;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Cast;
using Serilog.Events;

namespace NoMercy.Api.Hubs;

public class VideoHub : ConnectionHub
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
        _networkDiscovery = networkDiscovery;
    }

    public async Task SetTime(VideoProgressRequest request)
    {
        Guid userId = Context.User.UserId();

        User? user = ClaimsPrincipalExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null)
            return;

        await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();

        bool videoFileExists = await mediaContext.VideoFiles.AnyAsync(v => v.Id == request.VideoId);
        if (!videoFileExists)
            return;

        int? movieId = request.PlaylistType == MediaTypes.MovieMediaType ? request.TmdbId : null;
        int? tvId = request.PlaylistType == MediaTypes.TvMediaType ? request.TmdbId : null;

        int? collectionId = null;
        if (request.PlaylistType == MediaTypes.CollectionMediaType)
        {
            if (!int.TryParse(request.PlaylistId, out int parsed))
                return;
            collectionId = parsed;
        }

        Ulid? specialId = null;
        if (request.PlaylistType == MediaTypes.SpecialMediaType)
        {
            if (!Ulid.TryParse(request.PlaylistId, out Ulid parsed))
                return;
            specialId = parsed;
        }

        if (movieId is not null && !await mediaContext.Movies.AnyAsync(m => m.Id == movieId))
            return;
        if (tvId is not null && !await mediaContext.Tvs.AnyAsync(t => t.Id == tvId))
            return;
        if (
            collectionId is not null
            && !await mediaContext.Collections.AnyAsync(c => c.Id == collectionId)
        )
            return;
        if (specialId is not null && !await mediaContext.Specials.AnyAsync(s => s.Id == specialId))
            return;

        UserData userdata = new()
        {
            Audio = request.Audio,
            Subtitle = request.Subtitle,
            SubtitleType = request.SubtitleType,
            UserId = user.Id,
            Type = request.PlaylistType,
            Time = request.Time,
            VideoFileId = request.VideoId,
            MovieId = movieId,
            TvId = tvId,
            CollectionId = collectionId,
            SpecialId = specialId,
        };

        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(userdata);

        query = request.PlaylistType switch
        {
            MediaTypes.MovieMediaType => query.On(x => new
            {
                x.VideoFileId,
                x.UserId,
                x.MovieId,
            }),
            MediaTypes.TvMediaType => query.On(x => new
            {
                x.VideoFileId,
                x.UserId,
                x.TvId,
            }),
            MediaTypes.CollectionMediaType => query.On(x => new
            {
                x.VideoFileId,
                x.UserId,
                x.CollectionId,
            }),
            MediaTypes.SpecialMediaType => query.On(x => new
            {
                x.VideoFileId,
                x.UserId,
                x.SpecialId,
            }),
            _ => throw new ArgumentException("Invalid playlist type", request.PlaylistType),
        };

        await query
            .WhenMatched(
                (uds, udi) =>
                    new()
                    {
                        Id = uds.Id,
                        Type = udi.Type,
                        MovieId = udi.MovieId,
                        TvId = udi.TvId,
                        CollectionId = udi.CollectionId,
                        SpecialId = udi.SpecialId,
                        Time = udi.Time,
                        Audio = udi.Audio,
                        Subtitle = udi.Subtitle,
                        SubtitleType = udi.SubtitleType,
                        LastPlayedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    }
            )
            .RunAsync();
    }

    public async Task RemoveWatched(VideoProgressRequest request)
    {
        string? guid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(guid, out Guid userId))
            return;

        User? user = ClaimsPrincipalExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null)
            return;

        await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();
        UserData[] userdata = await mediaContext
            .UserData.Where(x => x.UserId == user.Id)
            .Where(x => x.Type == request.PlaylistType)
            .Where(x =>
                x.MovieId == request.TmdbId
                || x.TvId == request.TmdbId
                || x.SpecialId == request.SpecialId
                || x.CollectionId == request.TmdbId
            )
            .ToArrayAsync();

        mediaContext.UserData.RemoveRange(userdata);

        await mediaContext.SaveChangesAsync();
    }

    private static readonly ConcurrentDictionary<Guid, Device> CurrentDevice = new();

    public async Task StartPlaybackCommand(string? type, dynamic? listId, int? itemId)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        if (string.IsNullOrEmpty(type) || listId is null)
        {
            Logger.Socket(
                $"{user.Name}: [VideoHub.StartPlaybackCommand] ignored — null arg "
                    + $"(type='{type ?? "<null>"}', listId={(listId is null ? "<null>" : "set")})",
                LogEventLevel.Warning
            );
            return;
        }

        string language = GetLanguageFromContext();
        string country = GetCountryFromContext();

        try
        {
            dynamic? playlistResult = await _videoPlaylistManager.GetPlaylist(
                user.Id,
                type,
                listId,
                itemId,
                language,
                country
            );

            await HandlePlaybackState(
                user,
                type,
                listId,
                playlistResult.Item1,
                playlistResult.Item2
            );
        }
        catch (ArgumentException ex)
        {
            Logger.App($"Invalid playlist type: {ex.Message}");

            User? user2 = Context.User.User();
            if (user2 is not null)
            {
                ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client2);
                Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
                try
                {
                    await ActivityLogger.LogFailureAsync(
                        "failure.playback_start",
                        user2.Id,
                        deviceId2,
                        errorCode: ex.GetType().Name,
                        message: ex.Message
                    );
                }
                catch (Exception logEx)
                {
                    Logger.Socket(
                        $"Failed to log failure.playback_start: {logEx.Message}",
                        LogEventLevel.Warning
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App("Error in StartPlaybackCommand");
            Logger.App(ex);

            User? user2 = Context.User.User();
            if (user2 is not null)
            {
                ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client2);
                Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
                try
                {
                    await ActivityLogger.LogFailureAsync(
                        "failure.playback_start",
                        user2.Id,
                        deviceId2,
                        errorCode: ex.GetType().Name,
                        message: ex.Message
                    );
                }
                catch (Exception logEx)
                {
                    Logger.Socket(
                        $"Failed to log failure.playback_start: {logEx.Message}",
                        LogEventLevel.Warning
                    );
                }
            }
        }
    }

    private async Task HandlePlaybackState(
        User user,
        string type,
        dynamic listId,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist
    )
    {
        VideoPlayerState? playerState = _videoPlayerStateManager.GetState(user.Id);

        if (
            playerState is null
            || playerState.CurrentItem is null
            || playerState.Playlist.Count == 0
        )
            await HandleNewPlayerState(user, type, listId, item, playlist);
        else if (IsCurrentPlaylist(playerState, type, listId, item.Id))
            await HandleExistingPlaylistState(user, playerState);
        else
            await HandlePlaylistChange(user, playerState, type, listId, item, playlist);
    }

    private async Task HandleNewPlayerState(
        User user,
        string type,
        dynamic listId,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist
    )
    {
        Device device = GetCurrentDevice(user);
        VideoPlayerState videoPlayerState = await VideoPlayerStateFactory.Create(
            _contextFactory,
            user,
            device,
            item,
            playlist,
            type,
            listId
        );

        _videoPlayerStateManager.UpdateState(user.Id, videoPlayerState);
        _videoPlaybackService.StartPlaybackTimer(user);
        await _videoPlaybackService.UpdatePlaybackState(user, videoPlayerState);
        await _videoPlaybackService.PublishStartedEventAsync(user.Id, videoPlayerState);

        try
        {
            await ActivityLogger.LogPlaybackAsync(
                "playback.started",
                user.Id,
                device.Id,
                item.VideoId,
                new { media_type = "video", title = item.Title }
            );
        }
        catch (Exception ex)
        {
            Logger.Socket($"Failed to log playback.started: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private Device GetCurrentDevice(User user)
    {
        if (CurrentDevice.TryGetValue(user.Id, out Device? device))
            return device;

        device = ConnectedClients.Clients.FirstOrDefault(d => d.Key == Context.ConnectionId).Value;
        CurrentDevice[user.Id] = device;

        return device;
    }

    private static bool IsCurrentPlaylist(
        VideoPlayerState state,
        string type,
        dynamic listId,
        int itemId
    )
    {
        return state.CurrentItem is not null
            && state.CurrentList.ToString().Contains($"{type}/{listId}")
            && state.CurrentItem?.Id == itemId;
    }

    private async Task HandleExistingPlaylistState(User user, VideoPlayerState state)
    {
        state.PlayState = true;

        state.Time = state.CurrentItem?.Progress?.Time * 1000 ?? 0;

        state.Actions.Disallows.Resuming = state.PlayState;
        state.Actions.Disallows.Pausing = !state.PlayState;
        state.Actions.Disallows.Stopping = false;
        state.Actions.Disallows.Seeking = false;
        state.Actions.Disallows.Muting = false;
        state.Actions.Disallows.Previous =
            state.CurrentItem is null || state.Playlist.IndexOf(state.CurrentItem) == 0;
        state.Actions.Disallows.Next =
            state.CurrentItem is null
            || state.Playlist.IndexOf(state.CurrentItem) == state.Playlist.Count - 1;

        _videoPlaybackService.StartPlaybackTimer(user);
        UpdateDeviceInfo(state);
        await _videoPlaybackService.UpdatePlaybackState(user, state);
        await _videoPlaybackService.PublishStartedEventAsync(user.Id, state);
    }

    private async Task HandlePlaylistChange(
        User user,
        VideoPlayerState state,
        string type,
        dynamic listId,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist
    )
    {
        UpdateDeviceInfo(state);
        UpdatePlaylistInfo(state, type, listId, item, playlist);

        _videoPlaybackService.StartPlaybackTimer(user);
        await _videoPlaybackService.UpdatePlaybackState(user, state);
        await _videoPlaybackService.PublishStartedEventAsync(user.Id, state);

        Device device = GetCurrentDevice(user);
        try
        {
            await ActivityLogger.LogPlaybackAsync(
                "playback.started",
                user.Id,
                device.Id,
                item.VideoId,
                new { media_type = "video", title = item.Title }
            );
        }
        catch (Exception ex)
        {
            Logger.Socket($"Failed to log playback.started: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private void UpdateDeviceInfo(VideoPlayerState state)
    {
        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device))
            return;
        state.DeviceId = device.DeviceId;
        state.VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent;
    }

    private void UpdatePlaylistInfo(
        VideoPlayerState state,
        string type,
        dynamic listId,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist
    )
    {
        state.CurrentItem = item;
        state.PlayState = true;
        state.Playlist = playlist;
        state.CurrentList = new($"/{type}/{listId}/watch", UriKind.Relative);
        state.Time = item.Progress?.Time * 1000 ?? 0;
        state.Duration = item.Duration.ToMilliSeconds();
        state.Actions = new()
        {
            Disallows = new()
            {
                Stopping = false,
                Seeking = false,
                Muting = false,
                Pausing = !state.PlayState,
                Resuming = state.PlayState,
                Previous = playlist.IndexOf(item) == 0,
                Next = playlist.IndexOf(item) == playlist.Count - 1,
            },
        };
    }

    public VideoPlayerState? GetStateCommand()
    {
        User? user = Context.User.User();
        if (user is null)
            return null;

        _videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? playerState);
        if (playerState is null)
            return null;

        return playerState;
    }

    public async Task PlaybackCommand(string? command, object? data = null)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        if (string.IsNullOrEmpty(command))
        {
            Logger.Socket(
                $"{user.Name}: [VideoHub.PlaybackCommand] ignored — command was null/empty",
                LogEventLevel.Warning
            );
            return;
        }

        if (!_videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? state))
        {
            await _videoPlaybackService.UpdatePlaybackState(user, null);
            return;
        }

        ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device);

        await _commandHandler.HandleCommand(user, command, data, state, device);

        if (state.DeviceId == null)
            if (device is not null)
            {
                state.DeviceId = device.DeviceId;
                state.VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent;
            }

        await _videoPlaybackService.UpdatePlaybackState(user, state);
    }

    public async Task ChangeDeviceCommand(string? deviceId)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        if (string.IsNullOrEmpty(deviceId))
        {
            Logger.Socket(
                $"{user.Name}: [VideoHub.ChangeDeviceCommand] ignored — deviceId was null/empty",
                LogEventLevel.Warning
            );
            return;
        }

        // Extend connected-device list with owned TVs from the Devices table —
        // mirrors MusicHub.MusicDevicesAsync. Without this, the picker can't
        // hand video off to a sleeping TV. Live MusicHub clients are merged
        // with registered TV devices (online or not).
        List<Device> connectedDevices = Devices();
        await using (MediaContext ctx = await _contextFactory.CreateDbContextAsync())
        {
            List<Device> registeredTvs = await ctx
                .Devices.Where(d => d.OwnerUserId == user.Id && d.Type == "tv")
                .ToListAsync();

            HashSet<string> seenDeviceIds = new(
                connectedDevices.Select(d => d.DeviceId),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (Device tv in registeredTvs)
                if (seenDeviceIds.Add(tv.DeviceId))
                    connectedDevices.Add(tv);
        }

        await _clientMessenger.SendTo(
            "ConnectedDevicesState",
            "videoHub",
            user.Id,
            connectedDevices
        );

        // TV-target branch: when handing off video to a TV, mint a cast session
        // bundle and LAUNCH the receiver. Mirrors MusicHub.ChangeDeviceCommand.
        // Cast Connect routes APK-installed TVs to the native APK and Web-only
        // TVs to cast.nomercy.tv — both consume customData.
        Device? targetTv = connectedDevices.FirstOrDefault(d =>
            d.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase) && d.Type == "tv"
        );

        if (targetTv is not null)
        {
            string targetIp = targetTv.Ip;
            Ulid targetUlid = targetTv.Id;
            string serverIdString = Info.DeviceId.ToString();
            string serverUrl = ResolveServerUrl();
            string locale = ResolveSenderLocale();
            CastIntent intent = ResolveVideoIntent(user.Id);

            _ = Task.Run(async () =>
            {
                try
                {
                    string? receiverName = await ChromeCast.FindReceiverNameByIpAsync(targetIp);
                    if (string.IsNullOrEmpty(receiverName))
                    {
                        Logger.Socket(
                            $"No Chromecast receiver discovered at {targetIp} — video handoff will not wake panel via CEC",
                            LogEventLevel.Warning
                        );
                        return;
                    }

                    LaunchCustomData? launchData = await _castTokenService.MintAsync(
                        userId: user.Id,
                        serverId: serverIdString,
                        serverUrl: serverUrl,
                        deviceId: targetUlid,
                        intent: intent,
                        clientLocale: locale
                    );

                    if (launchData is null)
                    {
                        Logger.Socket(
                            $"Cast token mint failed for video handoff to {targetIp} — falling back to LAUNCH without customData",
                            LogEventLevel.Warning
                        );
                    }

                    bool apkOnline = _busRegistry.IsOnline(targetUlid);
                    await ChromeCast.SelectChromecast(receiverName);
                    await ChromeCast.LaunchAndroidReceiver(
                        receiverName,
                        launchData,
                        useAndroidReceiver: apkOnline
                    );
                }
                catch (Exception ex)
                {
                    Logger.Socket(
                        $"Server-side video Cast launch failed for {targetIp}: {ex.Message}",
                        LogEventLevel.Warning
                    );
                }
            });
        }

        if (_videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? playerState))
        {
            playerState.DeviceId = deviceId;
        }
        else
        {
            await _videoPlaybackService.UpdatePlaybackState(user, playerState);
            return;
        }

        EventPayload<BroadcastEventPayload> payload = new()
        {
            Events =
            [
                new()
                {
                    DeviceBroadcastStatus = new()
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        BroadcastStatus = VideoEventType.BroadcastUnavailable,
                        DeviceId = deviceId,
                    },
                },
            ],
        };

        await _clientMessenger.SendTo("ChangeDevice", "videoHub", user.Id, payload);
    }

    // ── Cast-receiver helpers (Phase 0) ──────────────────────────────────────

    private string ResolveServerUrl()
    {
        string? external = _networkDiscovery?.ExternalAddress;
        return string.IsNullOrEmpty(external) ? ExternalServicesConfig.Current.ApiBaseUrl : external;
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
        User? user = Context.User.User();
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
