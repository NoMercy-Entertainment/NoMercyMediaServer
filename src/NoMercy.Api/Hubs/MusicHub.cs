using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Hubs;

public class MusicHub : ConnectionHub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MusicPlaybackService _musicPlaybackService;
    private readonly MusicPlayerStateManager _musicPlayerStateManager;
    private readonly MusicDeviceManager _musicDeviceManager;
    private readonly MusicPlaylistManager _musicPlaylistManager;
    private readonly MusicPlaybackCommandHandler _commandHandler;

    public MusicHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        MusicPlaybackService musicPlaybackService,
        MusicPlayerStateManager musicPlayerStateManager,
        MusicDeviceManager musicDeviceManager,
        MusicPlaylistManager musicPlaylistManager,
        MusicPlaybackCommandHandler commandHandler)
        : base(httpContextAccessor, contextFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _musicPlaybackService = musicPlaybackService;
        _musicPlayerStateManager = musicPlayerStateManager;
        _musicDeviceManager = musicDeviceManager;
        _musicPlaylistManager = musicPlaylistManager;
        _commandHandler = commandHandler;
    }

    private static readonly ConcurrentDictionary<Guid, Device> CurrentDevice = new();

    public async Task StartPlaybackCommand(string type, Guid listId, Guid trackId)
    {
        User? user = Context.User.User();
        if (user is null) return;

        string country = GetCountryFromContext();

        try
        {
            (PlaylistTrackDto item, List<PlaylistTrackDto> playlist) =
                await _musicPlaylistManager.GetPlaylist(user.Id, type, listId, trackId, country);
            await HandlePlaybackState(user, type, listId, item, playlist);
        }
        catch (ArgumentException ex)
        {
            Logger.App($"Invalid playlist type: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.App($"Error in StartPlaybackCommand: {ex.Message}");
        }
    }

    private async Task HandlePlaybackState(User user, string type, Guid listId, PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist)
    {
        MusicPlayerState? playerState = _musicPlayerStateManager.GetState(user.Id);

        // Special handling for type="track" - only works with existing player state
        if (type.ToLower().Trim() == "track")
        {
            if (playerState?.CurrentItem is null)
            {
                // No active player state, cannot reorder - log and return
                Logger.App("Cannot play track: No active playlist");
                return;
            }
            await HandleTrackReorder(user, playerState, item);
            return;
        }

        // Normal playlist handling
        if (playerState?.CurrentItem is null || playerState.Playlist.Count == 0)
            await HandleNewPlayerState(user, type, listId, item, playlist);
        else if (IsCurrentPlaylist(playerState, type, listId, item.Id))
            await HandleExistingPlaylistState(user, playerState);
        else
            await HandlePlaylistChange(user, playerState, type, listId, item, playlist);
    }

    private async Task HandleNewPlayerState(User user, string type, Guid listId, PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist)
    {
        Device device = GetCurrentDevice(user);
        MusicPlayerState musicPlayerState = MusicPlayerStateFactory.Create(device, item, playlist, type, listId);

        _musicPlayerStateManager.UpdateState(user.Id, musicPlayerState);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, musicPlayerState);
        await _musicPlaybackService.PublishStartedEventAsync(user.Id, musicPlayerState);
    }

    private Device GetCurrentDevice(User user)
    {
        Client device = Networking.Networking.SocketClients
            .First(d => d.Key == Context.ConnectionId).Value;
    
        CurrentDevice[user.Id] = device;

        return device;
    }

    private static bool IsCurrentPlaylist(MusicPlayerState state, string type, Guid listId, Guid itemId)
    {
        return state.CurrentItem is not null && state.CurrentList.ToString().Contains($"{type}/{listId}") &&
               state.CurrentItem?.Id == itemId;
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
        // Check if it's the current item
        if (state.CurrentItem?.Id == item.Id)
        {
            // Already playing this track, just restart it
            state.Time = 0;
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
                state.PlayState = true;
                state.Duration = item.Duration.ToMilliSeconds();
            }
            else
            {
                // Track not found in current queue at all
                Logger.App($"Track {item.Id} not found in current queue");
                return;
            }
        }
        
        UpdateActionsDisallows(state);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
    }

    private async Task HandlePlaylistChange(User user, MusicPlayerState state, string type, Guid listId,
        PlaylistTrackDto item, List<PlaylistTrackDto> playlist)
    {
        UpdateDeviceInfo(state);
        UpdatePlaylistInfo(state, type, listId, item, playlist);
        UpdateActionsDisallows(state);

        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
        await _musicPlaybackService.PublishStartedEventAsync(user.Id, state);
    }

    private void UpdateDeviceInfo(MusicPlayerState state)
    {
        if (!Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? device)) return;
        state.DeviceId = device.DeviceId;
        state.VolumePercentage = device.VolumePercent;
    }

    private void UpdatePlaylistInfo(MusicPlayerState state, string type, Guid listId, PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist)
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
                TogglingRepeatTrack = false
            }
        };
    }

    public MusicPlayerState? GetStateCommand()
    {
        User? user = Context.User.User();
        if (user is null) return null;

        _musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState);
        if (playerState is null) return null;

        return playerState;
    }

    public async Task PlaybackCommand(string command, object? data = null)
    {
        User? user = Context.User.User();
        if (user is null) return;

        if (!_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? state))
        {
            await _musicPlaybackService.UpdatePlaybackState(user, null);
            return;
        }

        _commandHandler.HandleCommand(user, command, data, state);

        if (state.DeviceId == null)
            if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? device))
            {
                state.DeviceId = device.DeviceId;
                state.VolumePercentage = device.VolumePercent;
            }

        UpdateActionsDisallows(state);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
    }

    public async Task CurrentTimeCommand(int time)
    {
        User? user = Context.User.User();
        if (user is null) return;

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            playerState.Time = time * 1000;

            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
    }

    public async Task ChangeDeviceCommand(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null) return;

        List<Device> connectedDevices = Devices();

        await Networking.Networking.SendTo("ConnectedDevicesState", "musicHub", user.Id, connectedDevices);

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            playerState.DeviceId = deviceId;
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
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
                        BroadcastStatus = MusicEventType.BroadcastUnavailable,
                        DeviceId = deviceId
                    }
                }
            ]
        };

        await Networking.Networking.SendTo("ChangeDevice", "musicHub", user.Id, payload);
    }

    public async Task ChangeVolumeCommand(int volume)
    {
        User? user = Context.User.User();
        if (user is null) return;

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            playerState.VolumePercentage = volume;
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
            return;
        }

        if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? client))
            if (CurrentDevice.TryGetValue(user.Id, out Device? device) && device.DeviceId == client.DeviceId)
            {
                device.VolumePercent = volume;

                await using MediaContext mediaContext = new();
                await mediaContext.Devices
                    .Where(d => d.DeviceId == device.DeviceId)
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
            }
    }

    private async Task LikeEvent(MusicLikeEventDto musicLikeEvent)
    {
        User? user = musicLikeEvent.User;
        if (user is null) return;

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            if (playerState.CurrentItem != null && playerState.CurrentItem.Id == musicLikeEvent.Id)
                playerState.CurrentItem.Favorite = musicLikeEvent.Liked;

            foreach (PlaylistTrackDto track in playerState.Playlist)
                if (track.Id == musicLikeEvent.Id)
                    track.Favorite = musicLikeEvent.Liked;

            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
    }

    private void OnLikeEvent(object? sender, MusicLikeEventDto e)
    {
        LikeEvent(e).Wait();
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        User? user = Context.User.User();
        if (user is null) return;

        await Task.Delay(3000);

        // Send updated device list to all connected devices for this user
        List<Device> connectedDevices = Devices();
        await Networking.Networking.SendTo("ConnectedDevicesState", "musicHub", user.Id, connectedDevices);

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            UpdateActionsDisallows(playerState);
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, new());

            // Subscribe to the OnLikeEvent
            AlbumsController.OnLikeEvent += OnLikeEvent;
            ArtistsController.OnLikeEvent += OnLikeEvent;
            TracksController.OnLikeEvent += OnLikeEvent;
        }

        Logger.Socket("Music client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        User? user = Context.User.User();
        if (user == null) return;

        bool stopPlayback = false;
        bool wasCurrentDevice = false;

        if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? client))
            if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? state))
                if (state.DeviceId == client.DeviceId)
                {
                    _musicPlaybackService.RemoveTimer(user.Id);

                    _musicDeviceManager.RemoveUserDevice(user.Id);

                    stopPlayback = true;
                    wasCurrentDevice = true;
                }

        await base.OnDisconnectedAsync(exception);

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            List<Device> connectedDevices = Devices();

            // Send updated device list to all remaining connected devices
            await Networking.Networking.SendTo("ConnectedDevicesState", "musicHub", user.Id, connectedDevices);

            if (connectedDevices.Count == 0)
            {
                CurrentDevice.TryRemove(user.Id, out _);
                
                // Unsubscribe from the OnLikeEvent
                AlbumsController.OnLikeEvent -= OnLikeEvent;
                ArtistsController.OnLikeEvent -= OnLikeEvent;
                TracksController.OnLikeEvent -= OnLikeEvent;

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
                        Seeking = true,
                        Stopping = true,
                        Muting = true,
                        TogglingShuffle = true,
                        TogglingRepeatContext = true,
                        TogglingRepeatTrack = true
                    }
                };
            }
            else if (stopPlayback)
            {
                // Remove current device if it was the disconnecting device
                if (wasCurrentDevice)
                {
                    CurrentDevice.TryRemove(user.Id, out _);
                }
                
                playerState.PlayState = false;
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Pausing = true,
                        Resuming = false,
                        Previous = playerState.CurrentItem == null || playerState.Backlog.Count <= 1,
                        Next = playerState.CurrentItem == null || 
                               (playerState.Playlist.IndexOf(playerState.CurrentItem) >= playerState.Playlist.Count - 1 && 
                                playerState.Repeat == "off"),
                        Seeking = false,
                        Stopping = false,
                        Muting = false,
                        TogglingShuffle = false,
                        TogglingRepeatContext = false,
                        TogglingRepeatTrack = false
                    }
                };
            }
        }

        await _musicPlaybackService.UpdatePlaybackState(user, playerState);

        Logger.Socket("Music client disconnected");
    }
}