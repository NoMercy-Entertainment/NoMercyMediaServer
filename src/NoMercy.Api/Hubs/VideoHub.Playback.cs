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
using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Http;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Cast;

namespace NoMercy.Api.Hubs;

public partial class VideoHub
{
    public async Task SetTime(VideoProgressRequest request)
    {
        Guid userId = Context.User.UserId();

        User? user = UserCacheService.Users.FirstOrDefault(predicate: x => x.Id.Equals(g: userId));

        if (user is null)
            return;

        await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();

        bool videoFileExists = await mediaContext.VideoFiles.AnyAsync(predicate: v => v.Id == request.VideoId);
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

        if (movieId is not null && !await mediaContext.Movies.AnyAsync(predicate: m => m.Id == movieId))
            return;
        if (tvId is not null && !await mediaContext.Tvs.AnyAsync(predicate: t => t.Id == tvId))
            return;
        if (
            collectionId is not null
            && !await mediaContext.Collections.AnyAsync(predicate: c => c.Id == collectionId)
        )
            return;
        if (specialId is not null && !await mediaContext.Specials.AnyAsync(predicate: s => s.Id == specialId))
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

        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(entity: userdata);

        query = request.PlaylistType switch
        {
            MediaTypes.MovieMediaType => query.On(match: x => new
            {
                x.VideoFileId,
                x.UserId,
                x.MovieId,
            }),
            MediaTypes.TvMediaType => query.On(match: x => new
            {
                x.VideoFileId,
                x.UserId,
                x.TvId,
            }),
            MediaTypes.CollectionMediaType => query.On(match: x => new
            {
                x.VideoFileId,
                x.UserId,
                x.CollectionId,
            }),
            MediaTypes.SpecialMediaType => query.On(match: x => new
            {
                x.VideoFileId,
                x.UserId,
                x.SpecialId,
            }),
            _ => throw new ArgumentException(message: "Invalid playlist type", paramName: request.PlaylistType),
        };

        await query
            .WhenMatched(
                updater: (uds, udi) =>
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
                        RemovedFromContinueWatching = false,
                    }
            )
            .RunAsync();
    }

    public async Task RemoveWatched(VideoProgressRequest request)
    {
        string? guid = Context.User?.FindFirstValue(claimType: ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(input: guid, result: out Guid userId))
            return;

        User? user = UserCacheService.Users.FirstOrDefault(predicate: x => x.Id.Equals(g: userId));

        if (user is null)
            return;

        // Scope the delete to the single requested item by its typed id. The old
        // predicate OR-ed MovieId/TvId/SpecialId/CollectionId == the request ids;
        // because a movie row has null Tv/Special/Collection ids (and vice versa),
        // a null request id matched EVERY row of that type — so finishing/removing
        // one item wiped the user's whole continue-watching list.
        int? intId = request.PlaylistType switch
        {
            MediaTypes.MovieMediaType or MediaTypes.TvMediaType or MediaTypes.CollectionMediaType =>
                request.TmdbId,
            _ => null,
        };
        Ulid? ulidId =
            request.PlaylistType == MediaTypes.SpecialMediaType ? request.SpecialId : null;

        await _userDataRepository.RemoveForItemAsync(userId: user.Id, type: request.PlaylistType, intId: intId, ulidId: ulidId);
    }

    public async Task StartPlaybackCommand(string? type, dynamic? listId, int? itemId)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;

        if (string.IsNullOrEmpty(value: type) || listId is null)
        {
            _logger.LogWarning(
                message: "{Name}: [VideoHub.StartPlaybackCommand] ignored — null arg (type='{Null}', listId={Set})", args: [user.Name, type ?? "<null>", (listId is null ? "<null>" : "set")]
            );
            return;
        }

        string language = GetLanguageFromContext();
        string country = GetCountryFromContext();

        try
        {
            dynamic? playlistResult = await _videoPlaylistManager.GetPlaylist(
                userId: user.Id,
                type: type,
                listId: listId,
                itemId: itemId,
                language: language,
                country: country
            );

            await HandlePlaybackState(
                user: user,
                type: type,
                listId: listId,
                item: playlistResult.Item1,
                playlist: playlistResult.Item2
            );
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation(message: "Invalid playlist type: {Message}", args: ex.Message);

            User? user2 = UserCacheService.GetUser(userId: Context.User.UserId());
            if (user2 is not null)
            {
                ConnectedClients.Clients.TryGetValue(key: Context.ConnectionId, value: out Client? client2);
                Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
                try
                {
                    await ActivityLogger.LogFailureAsync(
                        type: "failure.playback_start",
                        userId: user2.Id,
                        deviceId: deviceId2,
                        errorCode: ex.GetType().Name,
                        message: ex.Message
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(
                        message: "Failed to log failure.playback_start: {Message}",
                        args: logEx.Message
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(message: "Error in StartPlaybackCommand");
            _logger.LogError(exception: ex, message: ex.Message);

            User? user2 = UserCacheService.GetUser(userId: Context.User.UserId());
            if (user2 is not null)
            {
                ConnectedClients.Clients.TryGetValue(key: Context.ConnectionId, value: out Client? client2);
                Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
                try
                {
                    await ActivityLogger.LogFailureAsync(
                        type: "failure.playback_start",
                        userId: user2.Id,
                        deviceId: deviceId2,
                        errorCode: ex.GetType().Name,
                        message: ex.Message
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(
                        message: "Failed to log failure.playback_start: {Message}",
                        args: logEx.Message
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
        VideoPlayerState? playerState = _videoPlayerStateManager.GetState(userId: user.Id);

        if (
            playerState is null
            || playerState.CurrentItem is null
            || playerState.Playlist.Count == 0
        )
            await HandleNewPlayerState(user: user, type: type, listId: listId, item: item, playlist: playlist);
        else if (IsCurrentPlaylist(state: playerState, type: type, listId: listId, itemId: item.Id))
            await HandleExistingPlaylistState(user: user, state: playerState);
        else
            await HandlePlaylistChange(user: user, state: playerState, type: type, listId: listId, item: item, playlist: playlist);
    }

    private async Task HandleNewPlayerState(
        User user,
        string type,
        dynamic listId,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist
    )
    {
        Device device = GetCurrentDevice(user: user);
        VideoPlayerState videoPlayerState = await VideoPlayerStateFactory.Create(
            contextFactory: _contextFactory,
            user: user,
            device: device,
            item: item,
            playlist: playlist,
            type: type,
            listId: listId
        );

        _videoPlayerStateManager.UpdateState(userId: user.Id, state: videoPlayerState);
        _videoPlaybackService.StartPlaybackTimer(user: user);
        await _videoPlaybackService.UpdatePlaybackState(user: user, state: videoPlayerState);
        await _videoPlaybackService.PublishStartedEventAsync(userId: user.Id, state: videoPlayerState);

        try
        {
            await ActivityLogger.LogPlaybackAsync(
                type: "playback.started",
                userId: user.Id,
                deviceId: device.Id,
                mediaId: item.VideoId,
                metadata: new { media_type = "video", title = item.Title }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(message: "Failed to log playback.started: {Message}", args: ex.Message);
        }
    }

    private Device GetCurrentDevice(User user)
    {
        if (CurrentDevice.TryGetValue(key: user.Id, value: out Device? device))
            return device;

        device = ConnectedClients.Clients.FirstOrDefault(predicate: d => d.Key == Context.ConnectionId).Value;
        CurrentDevice[key: user.Id] = device;

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
            && state.CurrentList.ToString().Contains(value: $"{type}/{listId}")
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
            state.CurrentItem is null || state.Playlist.IndexOf(item: state.CurrentItem) == 0;
        state.Actions.Disallows.Next =
            state.CurrentItem is null
            || state.Playlist.IndexOf(item: state.CurrentItem) == state.Playlist.Count - 1;

        _videoPlaybackService.StartPlaybackTimer(user: user);
        UpdateDeviceInfo(state: state);
        await _videoPlaybackService.UpdatePlaybackState(user: user, state: state);
        await _videoPlaybackService.PublishStartedEventAsync(userId: user.Id, state: state);
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
        UpdateDeviceInfo(state: state);
        UpdatePlaylistInfo(state: state, type: type, listId: listId, item: item, playlist: playlist);

        _videoPlaybackService.StartPlaybackTimer(user: user);
        await _videoPlaybackService.UpdatePlaybackState(user: user, state: state);
        await _videoPlaybackService.PublishStartedEventAsync(userId: user.Id, state: state);

        Device device = GetCurrentDevice(user: user);
        try
        {
            await ActivityLogger.LogPlaybackAsync(
                type: "playback.started",
                userId: user.Id,
                deviceId: device.Id,
                mediaId: item.VideoId,
                metadata: new { media_type = "video", title = item.Title }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(message: "Failed to log playback.started: {Message}", args: ex.Message);
        }
    }

    private void UpdateDeviceInfo(VideoPlayerState state)
    {
        if (!ConnectedClients.Clients.TryGetValue(key: Context.ConnectionId, value: out Client? device))
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
        state.CurrentList = new(uriString: $"/{type}/{listId}/watch", uriKind: UriKind.Relative);
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
                Previous = playlist.IndexOf(item: item) == 0,
                Next = playlist.IndexOf(item: item) == playlist.Count - 1,
            },
        };
    }

    public VideoPlayerState? GetStateCommand()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return null;

        _videoPlayerStateManager.TryGetValue(userId: user.Id, state: out VideoPlayerState? playerState);
        if (playerState is null)
            return null;

        return playerState;
    }

    public async Task PlaybackCommand(string? command, object? data = null)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;

        if (string.IsNullOrEmpty(value: command))
        {
            _logger.LogWarning(
                message: "{Name}: [VideoHub.PlaybackCommand] ignored — command was null/empty",
                args: user.Name
            );
            return;
        }

        if (!_videoPlayerStateManager.TryGetValue(userId: user.Id, state: out VideoPlayerState? state))
        {
            await _videoPlaybackService.UpdatePlaybackState(user: user, state: null);
            return;
        }

        ConnectedClients.Clients.TryGetValue(key: Context.ConnectionId, value: out Client? device);

        await _commandHandler.HandleCommand(user: user, command: command, data: data, state: state, device: device);

        if (state.DeviceId == null)
            if (device is not null)
            {
                state.DeviceId = device.DeviceId;
                state.VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent;
            }

        await _videoPlaybackService.UpdatePlaybackState(user: user, state: state);
    }

    public async Task ChangeDeviceCommand(string? deviceId)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;

        if (string.IsNullOrEmpty(value: deviceId))
        {
            _logger.LogWarning(
                message: "{Name}: [VideoHub.ChangeDeviceCommand] ignored — deviceId was null/empty",
                args: user.Name
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
                .Devices.Where(predicate: d => d.OwnerUserId == user.Id && d.Type == "tv")
                .ToListAsync();

            HashSet<string> seenDeviceIds = new(
                collection: connectedDevices.Select(selector: d => d.DeviceId),
                comparer: StringComparer.OrdinalIgnoreCase
            );

            foreach (Device tv in registeredTvs)
                if (seenDeviceIds.Add(item: tv.DeviceId))
                    connectedDevices.Add(item: tv);
        }

        await _clientMessenger.SendTo(
            name: "ConnectedDevicesState",
            endpoint: "videoHub",
            userId: user.Id,
            data: connectedDevices
        );

        // TV-target branch: when handing off video to a TV, mint a cast session
        // bundle and LAUNCH the receiver. Mirrors MusicHub.ChangeDeviceCommand.
        // Cast Connect routes APK-installed TVs to the native APK and Web-only
        // TVs to cast.nomercy.tv — both consume customData.
        Device? targetTv = connectedDevices.FirstOrDefault(predicate: d =>
            d.DeviceId.Equals(value: deviceId, comparisonType: StringComparison.OrdinalIgnoreCase) && d.Type == "tv"
        );

        if (targetTv is not null)
        {
            string targetIp = targetTv.Ip;
            Ulid targetUlid = targetTv.Id;
            string serverIdString = Info.DeviceId.ToString();
            string serverUrl = ResolveServerUrl();
            string locale = ResolveSenderLocale();
            CastIntent intent = ResolveVideoIntent(userId: user.Id);

            _ = Task.Run(function: async () =>
            {
                try
                {
                    string? receiverName = await _chromeCast.FindReceiverNameByIpAsync(ip: targetIp);
                    if (string.IsNullOrEmpty(value: receiverName))
                    {
                        _logger.LogWarning(
                            message: "No Chromecast receiver discovered at {TargetIp} — video handoff will not wake panel via CEC",
                            args: targetIp
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
                        _logger.LogWarning(
                            message: "Cast token mint failed for video handoff to {TargetIp} — falling back to LAUNCH without customData",
                            args: targetIp
                        );
                    }

                    bool apkOnline = _busRegistry.IsOnline(deviceId: targetUlid);
                    await _chromeCast.SelectChromecast(name: receiverName);
                    await _chromeCast.LaunchAndroidReceiver(
                        name: receiverName,
                        customData: launchData,
                        useAndroidReceiver: apkOnline
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        message: "Server-side video Cast launch failed for {TargetIp}: {Message}", args: [targetIp, ex.Message]
                    );
                }
            });
        }

        if (_videoPlayerStateManager.TryGetValue(userId: user.Id, state: out VideoPlayerState? playerState))
        {
            playerState.DeviceId = deviceId;
        }
        else
        {
            await _videoPlaybackService.UpdatePlaybackState(user: user, state: playerState);
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

        await _clientMessenger.SendTo(name: "ChangeDevice", endpoint: "videoHub", userId: user.Id, data: payload);
    }
}
