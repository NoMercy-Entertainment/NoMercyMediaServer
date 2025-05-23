using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Networking;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using static System.Int32;

namespace NoMercy.Api.Controllers.Socket;

public enum EventType {
    Null,
    PlayerStateChanged,
    BroadcastUnavailable,
    DeviceStateChanged,
}

public class EventPayload<T>
{
    [JsonProperty("events", NullValueHandling = NullValueHandling.Ignore)] public List<T> Events { get; set; } = [];
}

public class PlayerStateEventElement
{
    [JsonProperty("event")] public PlayerStateEvent Event { get; set; } = null!;
    [JsonProperty("source")] public string Source { get; set; } = null!;
    [JsonProperty("type")] public EventType Type { get; set; } = EventType.Null;
    [JsonProperty("user")] public User User { get; set; } = null!;
}

public class PlayerStateEvent
{
    [JsonProperty("event_id")] public int EventId { get; set; }
    [JsonProperty("state")] public PlayerState? State { get; set; }
}

public class BroadcastEventPayload {
    [JsonProperty("deviceBroadcastStatus")] public DeviceBroadcastStatus DeviceBroadcastStatus { get; set; } = new();
}

public class DeviceBroadcastStatus
{
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("broadcast_status")] public EventType BroadcastStatus { get; set; } = EventType.Null;
    [JsonProperty("device_id")] public string DeviceId { get; set; } = null!;
}

public class MusicHub(IHttpContextAccessor httpContextAccessor) : ConnectionHub(httpContextAccessor)
{
    private readonly IHttpContextAccessor _httpContextAccessor1 = httpContextAccessor;
    
    private const int TimerInterval = 1000;
    private static int _playerStateEventId;
    
    private static readonly ConcurrentDictionary<Guid, PlayerState> PlayerStates = new();
    private static readonly ConcurrentDictionary<Guid, Device> CurrentDevice = new();
    private static readonly ConcurrentDictionary<Guid, Timer> PlaybackTimers = new();

    private static Int32 PlayerStateEventId =>  ++_playerStateEventId;

    private readonly string[] _repeatStates = [
        // "off",
        // "track",
        // "context"
        "off",
        "one",
        "all"
    ];

    private void StartPlaybackTimer(User user)
    {
        if (PlaybackTimers.TryGetValue(user.Id, out Timer? existingTimer))
        {
            existingTimer.Dispose();
        }

        if (!PlayerStates.TryGetValue(user.Id, out PlayerState? playerState)) return;

        Timer timer = new(_ =>
        {
            if (!PlayerStates.TryGetValue(user.Id, out playerState)) return;
            if (!playerState.PlayState || playerState.CurrentItem is null) return;

            playerState.Time += TimerInterval;
            
            int duration = playerState.CurrentItem.Duration.ToMilliSeconds();
    
            // Logger.App($"{playerState.Time}-{duration}");
            if (playerState.Time >= duration)
            {
                HandleTrackCompletion(user, playerState).Wait();
            }
        }, null, 2000, TimerInterval);

        PlaybackTimers[user.Id] = timer;
    }
    
    private async Task HandleTrackCompletion(User user, PlayerState state)
    {
        if (state.CurrentItem == null) return;

        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);

        // Handle different repeat modes
        switch (state.Repeat)
        {
            case "one":
                // Reset time and timestamp for repeat one
                state.Time = 0;
                state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                break;

            case "all" when currentIndex == state.Playlist.Count - 1:
                // Loop back to first track for repeat all
                state.Time = 0;
                state.CurrentItem = state.Playlist.FirstOrDefault();
                state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                break;

            default:
                // Move to next track if available
                if (currentIndex + 1 < state.Playlist.Count)
                {
                    state.PlayState = true;
                    state.Time = 0;
                    state.CurrentItem = state.Playlist[currentIndex + 1];
                    state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                else
                {
                    state.PlayState = false;
                    state.Time = 0;
                    state.CurrentItem = null;
                    state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                break;
        }

        // Update player state for all connected devices
        await UpdatePlaybackState(user, state);

        // Manage timer based on play state
        if (state.PlayState)
        {
            StartPlaybackTimer(user);
        }
        else if (PlaybackTimers.TryRemove(user.Id, out Timer? timer))
        {
            await timer.DisposeAsync();
        }
    }

    private async Task UpdatePlaybackState(User user, PlayerState? state)
    {
        EventPayload<PlayerStateEventElement> payload = new()
        {
            Events =
            [
                new()
                {
                    Event = new()
                    {
                        EventId = PlayerStateEventId,
                        State = state
                    },
                    Source = "musicHub",
                    Type = EventType.PlayerStateChanged,
                    User = user
                }
            ]
        };

        await Networking.Networking.SendTo("PlayerState", "musicHub", user.Id, payload);
    }
    
    public async Task StartPlaybackCommand(string type, Guid listId, Guid trackId)
    {
        User? user = Context.User.User();
        if (user is null) return;
        
        string country = _httpContextAccessor1.HttpContext?.Request.Headers["country"].FirstOrDefault() ?? "US";
        
        PlaylistTrackDto item = null!;
        List<PlaylistTrackDto> playlist = null!;

        await using MediaContext mediaContext = new();
        switch (type)
        {
            case "playlist":
            {
                PlaylistTrack? playlistTrack = mediaContext.PlaylistTrack
                    .Include(x => x.Track)
                    .ThenInclude(x => x.PlaylistTrack)
                    .ThenInclude(x => x.Track)
                    .ThenInclude(x => x.PlaylistTrack)
                    .ThenInclude(x => x.Track)
                    .FirstOrDefault(x => x.PlaylistId == listId && x.TrackId == trackId);

                if (playlistTrack is null) return;
                
                playlist = playlistTrack.Track.PlaylistTrack
                    .SelectMany(x => x.Track.PlaylistTrack)
                    .Select(x =>  new PlaylistTrackDto(x, country))
                    .ToList();
                
                item = playlist.First(p => p.Id == playlistTrack.TrackId);
                break;
            }
            case "album":
            {
                AlbumTrack? albumTrack = mediaContext.AlbumTrack
                    .Include(x => x.Track)
                    .ThenInclude(x => x.AlbumTrack)
                    .ThenInclude(x => x.Album)
                    .ThenInclude(x => x.AlbumTrack)
                    .ThenInclude(x => x.Track)
                    .ThenInclude(x => x.ArtistTrack)
                    .ThenInclude(x => x.Artist)
                    .FirstOrDefault(x => x.AlbumId == listId && x.TrackId == trackId);

                if (albumTrack is null) return;
                
                playlist = albumTrack.Track.AlbumTrack
                    .SelectMany(x => x.Album.AlbumTrack)
                    .Select(x =>  new PlaylistTrackDto(x, country))
                    .OrderBy(x => x.Disc)
                    .ThenBy(x => x.Track)
                    .ToList();
                
                item = playlist.First(p => p.Id == albumTrack.TrackId);
                break;
            }
            case "artist":
            {
                ArtistTrack? artistTrack = mediaContext.ArtistTrack
                    .Include(x => x.Track)
                    .ThenInclude(x => x.ArtistTrack)
                    .ThenInclude(x => x.Artist)
                    .ThenInclude(x => x.ArtistTrack)
                    .ThenInclude(x => x.Track)
                    .ThenInclude(track => track.AlbumTrack)
                    .ThenInclude(albumTrack => albumTrack.Album)
                    .ThenInclude(artist => artist.Translations)
                    .FirstOrDefault(x => x.ArtistId == listId && x.TrackId == trackId);

                if (artistTrack is null) return;

                playlist = artistTrack.Track.ArtistTrack
                    .SelectMany(x => x.Artist.ArtistTrack)
                    .Select(x =>  new PlaylistTrackDto(x, country))
                    .DistinctBy(x => x.Id)
                    .OrderBy(x => x.AlbumName)
                    .ThenBy(x => x.Disc)
                    .ThenBy(x => x.Track)
                    .ToList();
                
                item = playlist.First(p => p.Id == artistTrack.TrackId);
                break;
            }
        }
        
        
        PlayerState? playerState = PlayerStates.FirstOrDefault(d => d.Key == user.Id).Value;
        if (playerState is null)
        {
            Device? currentDevice = CurrentDevice.FirstOrDefault(d => d.Key == user.Id).Value;
            if (currentDevice == null)
            {
                currentDevice = Networking.Networking.SocketClients.FirstOrDefault(d => d.Key == Context.ConnectionId).Value;
                CurrentDevice[user.Id] = currentDevice;
            }
            
            playerState = new()
            {
                DeviceId = currentDevice.DeviceId,
                VolumePercentage = currentDevice.VolumePercent,
                CurrentItem = item,
                Playlist = playlist,
                PlayState = true,
                Time = 0,
                Duration = item.Duration.ToMilliSeconds(),
                Shuffle = false,
                Repeat = "off",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CurrentList = $"{type}/{listId}",
                Actions = new()
                {
                    Disallows = new()
                    {
                        Previous = false,
                        Resuming = false,
                    }
                },
            };
            
            PlayerStates.TryAdd(user.Id, playerState);
        }
        else if (playerState.CurrentItem is not null && playerState.CurrentList.Contains($"{type}/{listId}"))
        {
            playerState.PlayState = !playerState.PlayState;
        }
        else
        {
            if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? currentDevice))
            {
                playerState.DeviceId = currentDevice.DeviceId;
                playerState.VolumePercentage = currentDevice.VolumePercent;
            }

            playerState.CurrentItem = item;
            playerState.PlayState = true;
            playerState.Playlist = playlist;
            playerState.CurrentList = $"{type}/{listId}";
            playerState.Time = 0;
            playerState.Duration = item.Duration.ToMilliSeconds();
            playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            playerState.Actions = new()
            {
                Disallows = new()
                {
                    Previous = true,
                    Resuming = false,
                },
            };
        }
        
        StartPlaybackTimer(user);
        
        await UpdatePlaybackState(user, playerState);
    }

    public PlayerState? GetStateCommand()
    {
        User? user = Context.User.User();
        if (user is null) return null;

        PlayerStates.TryGetValue(user.Id, out PlayerState? playerState);
        if (playerState is null) return null;
        
        playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return playerState;
    }
    
    public async Task PlaybackCommand(string command, object? data = null)
    {
        User? user = Context.User.User();
        if (user is null) return;
        
        if (!PlayerStates.TryGetValue(user.Id, out PlayerState? playerState))
        {
            await UpdatePlaybackState(user, playerState);
            return;
        }
        
        int currentIndex = playerState.CurrentItem is not null 
            ? playerState.Playlist.IndexOf(playerState.CurrentItem) 
            : -1;

        switch (command.ToLower())
        {
            case "play":
                playerState.PlayState = true;
                StartPlaybackTimer(user);
                break;
            case "pause":
                playerState.PlayState = false;
                if (PlaybackTimers.TryRemove(user.Id, out Timer? timer))
                {
                    await timer.DisposeAsync();
                }
                break;
            case "seek":
                int seekTime = Parse(data?.ToString() ?? "0") * 1000;
                playerState.Time = seekTime;
                break;
            case "next":
                playerState.CurrentItem = playerState.Playlist.Skip(currentIndex + 1).FirstOrDefault();
                playerState.Time = 0;
                playerState.PlayState = true;
                break;
            case "previous":
                playerState.CurrentItem = playerState.Playlist.Skip(currentIndex - 1).FirstOrDefault();
                playerState.Time = 0;
                playerState.PlayState = true;
                break;
            case "shuffle":
                playerState.Shuffle = !playerState.Shuffle;
                break;
            case "repeat":
                playerState.Repeat = _repeatStates[(Array.IndexOf(_repeatStates, playerState.Repeat) + 1) % _repeatStates.Length];
                break;
            case "stop":
                playerState.DeviceId = null;
                playerState.CurrentItem = null;
                playerState.PlayState = false;
                playerState.Time = 0;
                playerState.Playlist = [];
                playerState.CurrentList = "";
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Previous = true,
                        Resuming = true,
                        Pausing = true,
                    }
                };
                break;
        }
        
        playerState.DeviceId ??= Networking.Networking.SocketClients.FirstOrDefault(d => d.Key == Context.ConnectionId).Value.DeviceId;
        playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await UpdatePlaybackState(user, playerState);
    }

    public async Task CurrentTimeCommand(int time)
    {
        User? user = Context.User.User();
        if (user is null) return;

        if (PlayerStates.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.Time = time * 1000;
            playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            await UpdatePlaybackState(user, playerState);
        }
        else 
        {
            await UpdatePlaybackState(user, playerState);
        }
    }
    
    public async Task ChangeDeviceCommand(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null) return;

        List<Device> connectedDevices = Devices();
        
        await Networking.Networking.SendTo("ConnectedDevicesState", "musicHub", user.Id, connectedDevices);

        if (PlayerStates.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.DeviceId = deviceId;
        }
        else
        {
            await UpdatePlaybackState(user, playerState);
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
        
        if (PlayerStates.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            playerState.VolumePercentage = volume;
            await UpdatePlaybackState(user, playerState);
        }
        else
        {
            await UpdatePlaybackState(user, playerState);
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
    
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        
        User? user = Context.User.User();
        if (user is null) return;
        
        await Task.Delay(3000);
        
        if (PlayerStates.TryGetValue(user.Id, out PlayerState? playerState))
        {
            playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await UpdatePlaybackState(user, playerState);
        }
        else
        {
            await UpdatePlaybackState(user, new());
            return;
        }
        
        Logger.Socket("Music client connected");
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        
        User? user = Context.User.User();
        if (user == null) return;

        if (!PlayerStates.TryGetValue(user.Id, out PlayerState? playerState)) return;
        
        if (Networking.Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? client))
        {
            if (CurrentDevice.TryGetValue(user.Id, out Device? device) && device.DeviceId == client.DeviceId)
            {
                if (PlaybackTimers.TryRemove(user.Id, out Timer? timer))
                {
                    await timer.DisposeAsync();
                }

                // Remove the user from the current device
                CurrentDevice.TryRemove(user.Id, out _);

                playerState.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                playerState.PlayState = false;
            }
        }
        
        List<Device> connectedDevices = Devices();
        
        if (connectedDevices.Count == 0)
        {
            playerState.DeviceId = null;
            // playerState.CurrentItem = null;
            playerState.PlayState = false;
            // playerState.Time = 0;
            // playerState.Playlist = [];
            // playerState.CurrentList = "";
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

        await UpdatePlaybackState(user, playerState);
        
        Logger.Socket("Music client disconnected");
    }

}