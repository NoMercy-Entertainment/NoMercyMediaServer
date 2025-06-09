using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.Socket.music;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Networking;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;

public class MusicHub : ConnectionHub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PlaybackService _playbackService;
    private readonly PlayerStateManager _playerStateManager;
    private readonly DeviceManager _deviceManager;
    private readonly PlaylistManager _playlistManager;
    private readonly PlaybackCommandHandler _commandHandler;

    public MusicHub(
        IHttpContextAccessor httpContextAccessor,
        PlaybackService playbackService,
        PlayerStateManager playerStateManager,
        DeviceManager deviceManager,
        PlaylistManager playlistManager,
        PlaybackCommandHandler commandHandler)
        : base(httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _playbackService = playbackService;
        _playerStateManager = playerStateManager;
        _deviceManager = deviceManager;
        _playlistManager = playlistManager;
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
            (PlaylistTrackDto item, List<PlaylistTrackDto> playlist) = await _playlistManager.GetPlaylist(type, listId, trackId, country);
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

    private async Task HandlePlaybackState(User user, string type, Guid listId, PlaylistTrackDto item, List<PlaylistTrackDto> playlist)
    {
        PlayerState? playerState = _playerStateManager.GetState(user.Id);
        
        if (playerState is null || playerState.CurrentItem is null || playerState.Playlist.Count == 0)
        {
            await HandleNewPlayerState(user, type, listId, item, playlist);
        }
        else if (IsCurrentPlaylist(playerState, type, listId, item.Id))
        {
            await HandleExistingPlaylistState(user, playerState);
        }
        else
        {
            await HandlePlaylistChange(user, playerState, type, listId, item, playlist);
        }
    }

    private async Task HandleNewPlayerState(User user, string type, Guid listId, PlaylistTrackDto item, List<PlaylistTrackDto> playlist)
    {
        Device device = GetCurrentDevice(user);
        PlayerState playerState = PlayerStateFactory.Create(device, item, playlist, type, listId);
        
        _playerStateManager.UpdateState(user.Id, playerState);
        _playbackService.StartPlaybackTimer(user);
        await _playbackService.UpdatePlaybackState(user, playerState);
    }

    private Device GetCurrentDevice(User user)
    {
        if (CurrentDevice.TryGetValue(user.Id, out Device? device))
            return device;

        device = Networking.Networking.SocketClients
            .FirstOrDefault(d => d.Key == Context.ConnectionId).Value;
        CurrentDevice[user.Id] = device;
        
        return device;
    }

    private static bool IsCurrentPlaylist(PlayerState state, string type, Guid listId, Guid itemId) =>
        state.CurrentItem is not null && state.CurrentList.Contains($"{type}/{listId}") && state.CurrentItem?.Id == itemId;

    private async Task HandleExistingPlaylistState(User user, PlayerState state)
    {
        state.PlayState = !state.PlayState;
        _playbackService.StartPlaybackTimer(user);
        await _playbackService.UpdatePlaybackState(user, state);
    }

    private async Task HandlePlaylistChange(User user, PlayerState state, string type, Guid listId, PlaylistTrackDto item, List<PlaylistTrackDto> playlist)
    {
        UpdateDeviceInfo(state);
        UpdatePlaylistInfo(state, type, listId, item, playlist);
        
        _playbackService.StartPlaybackTimer(user);
        await _playbackService.UpdatePlaybackState(user, state);
    }

    private void UpdateDeviceInfo(PlayerState state)
    {
        if (!Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? device)) return;
        state.DeviceId = device.DeviceId;
        state.VolumePercentage = device.VolumePercent;
    }

    private void UpdatePlaylistInfo(PlayerState state, string type, Guid listId, PlaylistTrackDto item, List<PlaylistTrackDto> playlist)
    {
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = _playlistManager.SplitPlaylist(playlist, item.Id);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);
        
        state.CurrentItem = item;
        state.PlayState = true;
        state.Playlist = sortedPlaylist;
        state.CurrentList = $"{type}/{listId}";
        state.Backlog.Add(item);
        state.Time = 0;
        state.Duration = item.Duration.ToMilliSeconds();
        state.Actions = new()
        {
            Disallows = new()
            {
                Previous = true,
                Resuming = false,
            }
        };
    }

    public PlayerState? GetStateCommand()
    {
        User? user = Context.User.User();
        if (user is null) return null;

        _playerStateManager.TryGetValue(user.Id, out PlayerState? playerState);
        if (playerState is null) return null;
        
        return playerState;
    }
    
    public async Task PlaybackCommand(string command, object? data = null)
    {
        User? user = Context.User.User();
        if (user is null) return;

        if (!_playerStateManager.TryGetValue(user.Id, out PlayerState? state))
        {
            await _playbackService.UpdatePlaybackState(user, null);
            return;
        }

        _commandHandler.HandleCommand(user, command, data, state);
        
        if (state.DeviceId == null)
        {
            if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? device))
            {
                state.DeviceId = device.DeviceId;
                state.VolumePercentage = device.VolumePercent;
            }
        }
        
        await _playbackService.UpdatePlaybackState(user, state);
    }
    
    public async Task CurrentTimeCommand(int time)
    {
        User? user = Context.User.User();
        if (user is null) return;

        if (_playerStateManager.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.Time = time * 1000;
            
            await _playbackService.UpdatePlaybackState(user, playerState);
        }
        else 
        {
            await _playbackService.UpdatePlaybackState(user, playerState);
        }
    }
    
    public async Task ChangeDeviceCommand(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null) return;

        List<Device> connectedDevices = Devices();
        
        await Networking.Networking.SendTo("ConnectedDevicesState", "musicHub", user.Id, connectedDevices);

        if (_playerStateManager.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.DeviceId = deviceId;
        }
        else
        {
            await _playbackService.UpdatePlaybackState(user, playerState);
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
                        BroadcastStatus = EventType.BroadcastUnavailable,
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
        
        if (_playerStateManager.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.VolumePercentage = volume;
            await _playbackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _playbackService.UpdatePlaybackState(user, playerState);
            return;
        }
        
        if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? client))
        {
            if (CurrentDevice.TryGetValue(user.Id, out Device? device) && device.DeviceId == client.DeviceId)
            {
                device.VolumePercent = volume;
                
                await using MediaContext mediaContext = new();
                await mediaContext.Devices
                    .Where(d => d.DeviceId == device.DeviceId)
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
            }
        }
    }

    private async Task LikeEvent(LikeEventDto likeEvent)
    {
        User? user = likeEvent.User;
        if (user is null) return;

        if (_playerStateManager.TryGetValue(user.Id, out PlayerState? playerState))
        {
            if (playerState.CurrentItem != null && playerState.CurrentItem.Id == likeEvent.Id)
            {
                playerState.CurrentItem.Favorite = likeEvent.Liked;
            }

            foreach (PlaylistTrackDto track in playerState.Playlist)
            {
                if (track.Id == likeEvent.Id)
                {
                    track.Favorite = likeEvent.Liked;
                }
            }

            await _playbackService.UpdatePlaybackState(user, playerState);
        }
    }

    private void OnLikeEvent(object? sender, LikeEventDto e)
    {
        LikeEvent(e).Wait();
    }
    
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        
        User? user = Context.User.User();
        if (user is null) return;
        
        await Task.Delay(3000);
        
        if (_playerStateManager.TryGetValue(user.Id, out PlayerState? playerState))
        {
            await _playbackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _playbackService.UpdatePlaybackState(user, new());
        
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
        
        if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? client))
        {
            if (_playerStateManager.TryGetValue(user.Id, out PlayerState? state))
            {
                if(state.DeviceId == client.DeviceId)
                {
                    _playbackService.RemoveTimer(user.Id);

                    _deviceManager.RemoveUserDevice(user.Id);
                    
                    stopPlayback = true;
                }
            }
        }
        
        await base.OnDisconnectedAsync(exception);

        if (_playerStateManager.TryGetValue(user.Id, out PlayerState? playerState))
        {
            List<Device> connectedDevices = Devices();
            
            if (connectedDevices.Count == 0)
            {
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
                        Resuming = true,
                        Pausing = true,
                    }
                };
            }
            else if (stopPlayback)
            {
                playerState.PlayState = false;
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Previous = true,
                        Resuming = true,
                        Pausing = true,
                    }
                };
            }
        }

        await _playbackService.UpdatePlaybackState(user, playerState);
        
        Logger.Socket("Music client disconnected");
    }

}