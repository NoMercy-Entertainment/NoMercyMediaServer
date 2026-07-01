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

using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Authorization;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Http;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Hubs;

public partial class MusicHub
{
    public async Task StartPlaybackCommand(string? type, Guid? listId, Guid? trackId)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        // Guard: clients occasionally send null for one of these (e.g. an
        // artist with no tracks → trackId is undefined on the client side
        // → null on the wire). Without this check the SignalR-generated
        // invocation thunk NREs while unboxing null into the value-type
        // parameter, before the method body even runs.
        if (string.IsNullOrEmpty(type) || listId is null || trackId is null)
        {
            _logger.LogWarning(
                "{Name}: [MusicHub.StartPlaybackCommand] ignored — null arg (type='{Null}', listId={Null2}, trackId={Null3})",
                user.Name,
                type ?? "<null>",
                listId?.ToString() ?? "<null>",
                trackId?.ToString() ?? "<null>"
            );
            return;
        }

        SemaphoreSlim userLock = GetUserLock(user.Id);
        await userLock.WaitAsync();
        try
        {
            string country = GetCountryFromContext();

            System.Diagnostics.Stopwatch playlistStopwatch =
                System.Diagnostics.Stopwatch.StartNew();
            (PlaylistTrackDto item, List<PlaylistTrackDto> playlist) =
                await _musicPlaylistManager.GetPlaylist(
                    user.Id,
                    type,
                    listId.Value,
                    trackId.Value,
                    country
                );
            playlistStopwatch.Stop();
            _logger.Log(
                playlistStopwatch.ElapsedMilliseconds > 1000 ? LogLevel.Warning : LogLevel.Debug,
                "[MusicHub.StartPlaybackCommand] GetPlaylist({Type}) took {ElapsedMilliseconds}ms ({Count} tracks)",
                type,
                playlistStopwatch.ElapsedMilliseconds,
                playlist.Count
            );

            await HandlePlaybackState(user, type, listId.Value, item, playlist);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation("Invalid playlist type: {Message}", ex.Message);

            ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client2);
            Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
            try
            {
                await ActivityLogger.LogFailureAsync(
                    "failure.playback_start",
                    user.Id,
                    deviceId2,
                    errorCode: ex.GetType().Name,
                    message: ex.Message
                );
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(
                    "Failed to log failure.playback_start: {Message}",
                    logEx.Message
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Error in StartPlaybackCommand: {Message}", ex.Message);

            ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client2);
            Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
            try
            {
                await ActivityLogger.LogFailureAsync(
                    "failure.playback_start",
                    user.Id,
                    deviceId2,
                    errorCode: ex.GetType().Name,
                    message: ex.Message
                );
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(
                    "Failed to log failure.playback_start: {Message}",
                    logEx.Message
                );
            }
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task HandlePlaybackState(
        User user,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        MusicPlayerState? playerState = _musicPlayerStateManager.GetState(user.Id);

        // Special handling for type="track" - only works with existing player state
        if (type.ToLower().Trim() == "track")
        {
            if (playerState?.CurrentItem is null)
            {
                // No active player state, cannot reorder - log and return
                _logger.LogInformation("Cannot play track: No active playlist");
                return;
            }
            await HandleTrackReorder(user, playerState, item);
            return;
        }

        // Normal playlist handling
        if (playerState?.CurrentItem is null || playerState.Playlist.Count == 0)
            await HandleNewPlayerState(user, type, listId, item, playlist);
        else if (IsSamePlaylistAndTrack(playerState, type, listId, item.Id))
            await HandleExistingPlaylistState(user, playerState);
        else if (IsSamePlaylist(playerState, type, listId))
            await HandleTrackReorder(user, playerState, item);
        else
            await HandlePlaylistChange(user, playerState, type, listId, item, playlist);
    }

    private async Task HandleNewPlayerState(
        User user,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        // GetOrPromoteActiveDevice respects an existing connected active
        // device, falling back to the caller only when there isn't one.
        // This prevents a passive sender (phone tapping a playlist after
        // a stop) from being promoted to active just because the previous
        // state had no CurrentItem — the active flag stays on the TV.
        Device device = GetOrPromoteActiveDevice(user);
        MusicPlayerState musicPlayerState = MusicPlayerStateFactory.Create(
            device,
            item,
            playlist,
            type,
            listId
        );
        musicPlayerState.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);

        _musicPlaybackService.RemoveTimer(user.Id);
        _musicPlayerStateManager.UpdateState(user.Id, musicPlayerState);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, musicPlayerState);
        await _musicPlaybackService.PublishStartedEventAsync(user.Id, musicPlayerState);

        try
        {
            await ActivityLogger.LogPlaybackAsync(
                "playback.started",
                user.Id,
                device.Id,
                Ulid.Empty,
                new
                {
                    media_type = "audio",
                    track_id = item.Id,
                    title = item.Name,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to log playback.started: {Message}", ex.Message);
        }
    }

    private static bool IsSamePlaylist(MusicPlayerState state, string type, Guid listId)
    {
        return state.CurrentItem is not null
            && state.CurrentList.ToString().Contains($"{type}/{listId}");
    }

    private static bool IsSamePlaylistAndTrack(
        MusicPlayerState state,
        string type,
        Guid listId,
        Guid itemId
    )
    {
        return IsSamePlaylist(state, type, listId) && state.CurrentItem?.Id == itemId;
    }

    private async Task HandleExistingPlaylistState(User user, MusicPlayerState state)
    {
        state.PlayState = !state.PlayState;
        UpdateActionsDisallows(state);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
        if (state.PlayState)
        {
            await _musicPlaybackService.PublishStartedEventAsync(user.Id, state);
        }
    }

    private async Task HandleTrackReorder(User user, MusicPlayerState state, PlaylistTrackDto item)
    {
        // Stop the old timer before modifying state to prevent race conditions
        _musicPlaybackService.RemoveTimer(user.Id);

        // Check if it's the current item
        if (state.CurrentItem?.Id == item.Id)
        {
            // Already playing this track, just restart it
            state.Time = 0;
            state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
            state.PlayState = true;
            UpdateActionsDisallows(state);
            _musicPlaybackService.StartPlaybackTimer(user);
            await _musicPlaybackService.UpdatePlaybackState(user, state);
            return;
        }

        // Find the track in the current playlist
        int playlistIndex = state.Playlist.FindIndex(t => t.Id == item.Id);

        if (playlistIndex != -1)
        {
            // Track is in the upcoming playlist
            // Add current item to backlog
            if (state.CurrentItem != null)
            {
                state.Backlog.Add(state.CurrentItem);
            }

            // Add all tracks BEFORE the selected one to backlog (they're being skipped over)
            for (int i = 0; i < playlistIndex; i++)
            {
                state.Backlog.Add(state.Playlist[i]);
            }

            // Remove everything up to and including the selected track
            state.Playlist.RemoveRange(0, playlistIndex + 1);

            // Set the selected track as current
            // The remaining playlist continues naturally from here
            state.CurrentItem = item;
            state.Time = 0;
            state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
            state.PlayState = true;
            state.Duration = item.Duration.ToMilliSeconds();
        }
        else
        {
            // Check if track is in backlog (going backwards)
            int backlogIndex = state.Backlog.FindIndex(t => t.Id == item.Id);

            if (backlogIndex != -1)
            {
                // Track is in backlog - going backwards
                // Remove it from backlog
                state.Backlog.RemoveAt(backlogIndex);

                // Add current item to backlog
                if (state.CurrentItem != null)
                {
                    state.Backlog.Add(state.CurrentItem);
                }

                // Set the selected track as current
                state.CurrentItem = item;
                state.Time = 0;
                state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
                state.PlayState = true;
                state.Duration = item.Duration.ToMilliSeconds();
            }
            else
            {
                // Track not found in current queue at all
                _logger.LogInformation("Track {Id} not found in current queue", item.Id);
                return;
            }
        }

        UpdateActionsDisallows(state);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
    }

    private async Task HandlePlaylistChange(
        User user,
        MusicPlayerState state,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        _musicPlaybackService.RemoveTimer(user.Id);

        UpdateDeviceInfo(state);
        UpdatePlaylistInfo(state, type, listId, item, playlist);
        UpdateActionsDisallows(state);

        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
        await _musicPlaybackService.PublishStartedEventAsync(user.Id, state);

        // Logging only — record who triggered the playlist change without
        // promoting them to active. The active flag is governed by
        // UpdateDeviceInfo, which respects an existing active device.
        Device device = GetCallerDevice(user);
        try
        {
            await ActivityLogger.LogPlaybackAsync(
                "playback.started",
                user.Id,
                device.Id,
                Ulid.Empty,
                new
                {
                    media_type = "audio",
                    track_id = item.Id,
                    title = item.Name,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to log playback.started: {Message}", ex.Message);
        }
    }

    private void UpdatePlaylistInfo(
        MusicPlayerState state,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) =
            _musicPlaylistManager.SplitPlaylist(playlist, item.Id);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);

        state.CurrentItem = item;
        state.PlayState = true;
        state.Playlist = sortedPlaylist;
        state.CurrentList = new($"/music/{type}/{listId}", UriKind.Relative);
        state.Backlog.Add(item);
        state.Time = 0;
        state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
        state.Duration = item.Duration.ToMilliSeconds();
    }

    private void UpdateActionsDisallows(MusicPlayerState state)
    {
        state.Actions = new()
        {
            Disallows = new()
            {
                // Can't pause if already paused
                Pausing = !state.PlayState,
                // Can't resume if already playing
                Resuming = state.PlayState,
                // Can't go to previous if backlog is empty (no tracks to go back to)
                Previous = state.Backlog.Count <= 0,
                // Can't go to next if playlist is empty and repeat is off
                Next = state.Playlist.Count <= 0 && state.Repeat == "off",
                // Basic actions that are always allowed during playback
                Seeking = false,
                Stopping = false,
                Muting = false,
                TogglingShuffle = false,
                TogglingRepeatContext = false,
                TogglingRepeatTrack = false,
            },
        };
    }

    public MusicPlayerState? GetStateCommand()
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return null;

        _musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState);
        if (playerState is null)
            return null;

        return playerState;
    }

    public async Task PlaybackCommand(string? command, object? data = null)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        if (string.IsNullOrEmpty(command))
        {
            _logger.LogWarning(
                "{Name}: [MusicHub.PlaybackCommand] ignored — command was null/empty",
                user.Name
            );
            return;
        }

        SemaphoreSlim userLock = GetUserLock(user.Id);
        await userLock.WaitAsync();
        try
        {
            if (!_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? state))
            {
                await _musicPlaybackService.UpdatePlaybackState(user, null);
                return;
            }

            _commandHandler.HandleCommand(user, command, data, state);

            if (state.DeviceId == null)
                if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device))
                {
                    state.DeviceId = device.DeviceId;
                    state.VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent;
                }

            UpdateActionsDisallows(state);

            bool isSkipCommand =
                command.Equals("next", StringComparison.OrdinalIgnoreCase)
                || command.Equals("previous", StringComparison.OrdinalIgnoreCase);

            if (isSkipCommand)
                _musicPlaybackService.DebouncedUpdatePlaybackState(user, state);
            else
                await _musicPlaybackService.UpdatePlaybackState(user, state);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task CurrentTimeCommand(int? time)
    {
        if (time is null)
            return;
        await ReportPositionCommand(time.Value * 1000);
    }

    public async Task ReportPositionCommand(int? positionMs)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        if (positionMs is null)
            return;

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            if (DateTime.UtcNow < playerState.IgnoreCurrentTimeUntil)
                return;

            playerState.Time = positionMs.Value;

            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
    }

    public long GetServerTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Called by the active client when it begins its crossfade volume ramp (typically 3 s
    /// before the current track ends).  Suppresses the server's 100 ms auto-advance timer for
    /// this user so the server does not race the client and interrupt the fade.
    /// </summary>
    /// <param name="fadeDurationMs">
    /// How long the client's fade takes in milliseconds (e.g. 3000).  The server adds a 5 s
    /// safety margin on top; if <c>CrossfadeCompleteCommand</c> never arrives within that window
    /// the server force-advances anyway.
    /// </param>
    public Task CrossfadeStartCommand(int? fadeDurationMs)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return Task.CompletedTask;

        if (fadeDurationMs is null)
            return Task.CompletedTask;

        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            return Task.CompletedTask;

        _musicPlaybackService.StartCrossfade(user.Id, client.DeviceId, fadeDurationMs.Value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the active client once its crossfade is complete and the new track is fully
    /// playing.  The server advances state to <paramref name="newTrackId"/>, resets progress to
    /// zero, and broadcasts the updated state to all connected clients.
    /// </summary>
    /// <param name="newTrackId">The <see cref="Guid"/> of the track that is now playing.</param>
    public async Task CrossfadeCompleteCommand(Guid? newTrackId)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        if (newTrackId is null)
        {
            _logger.LogWarning(
                "{Name}: [MusicHub.CrossfadeCompleteCommand] ignored — newTrackId was null",
                user.Name
            );
            return;
        }

        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            return;

        await _musicPlaybackService.CompleteCrossfade(user, client.DeviceId, newTrackId.Value);
    }
}
