using System.Collections.Concurrent;
using System.Security.Claims;
using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Services.Video;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

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
        VideoPlaybackCommandHandler commandHandler)
        : base(httpContextAccessor, contextFactory, connectedClients)
    {
        _httpContextAccessor = httpContextAccessor;
        _clientMessenger = clientMessenger;
        _contextFactory = contextFactory;
        _videoPlaybackService = videoPlaybackService;
        _videoPlayerStateManager = videoPlayerStateManager;
        _videoDeviceManager = videoDeviceManager;
        _videoPlaylistManager = videoPlaylistManager;
        _commandHandler = commandHandler;
    }
    
    public async Task SetTime(VideoProgressRequest request)
    {
        Guid userId = Context.User.UserId();

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null) return;

        UserData userdata = new()
        {
            Audio = request.Audio,
            Subtitle = request.Subtitle,
            SubtitleType = request.SubtitleType,
            UserId = user.Id,
            Type = request.PlaylistType,
            Time = request.Time,
            VideoFileId = request.VideoId,
            MovieId = request.PlaylistType == Config.MovieMediaType 
                ? request.TmdbId
                : null,
            TvId = request.PlaylistType == Config.TvMediaType
                ? request.TmdbId
                : null,
            CollectionId = request.PlaylistType == Config.CollectionMediaType
                ? int.Parse(request.PlaylistId) 
                : null,
            SpecialId = request.PlaylistType == Config.SpecialMediaType
                ? Ulid.Parse(request.PlaylistId) 
                : null
        };

        await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();

        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(userdata);

        query = request.PlaylistType switch
        {
            Config.MovieMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.MovieId }),
            Config.TvMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.TvId }),
            Config.CollectionMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.CollectionId }),
            Config.SpecialMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.SpecialId }),
            _ => throw new ArgumentException("Invalid playlist type", request.PlaylistType)
        };

        await query.WhenMatched((uds, udi) => new()
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
                LastPlayedDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            })
            .RunAsync();
    }

    public async Task RemoveWatched(VideoProgressRequest request)
    {
        Guid userId = Guid.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null) return;

        await using MediaContext mediaContext = await _contextFactory.CreateDbContextAsync();
        UserData[] userdata = await mediaContext.UserData
            .Where(x => x.UserId == user.Id)
            .Where(x => x.Type == request.PlaylistType)
            .Where(x => x.MovieId == request.TmdbId
                        || x.TvId == request.TmdbId
                        || x.SpecialId == request.SpecialId
                        || x.CollectionId == request.TmdbId)
            .ToArrayAsync();

        mediaContext.UserData.RemoveRange(userdata);

        await mediaContext.SaveChangesAsync();
    }
    
    private static readonly ConcurrentDictionary<Guid, Device> CurrentDevice = new();

    public async Task StartPlaybackCommand(string type, dynamic listId, int? itemId)
    {
        User? user = Context.User.User();
        if (user is null) return;

        string language = GetLanguageFromContext();
        string country = GetCountryFromContext();

        try
        {
            dynamic? playlistResult = await _videoPlaylistManager.GetPlaylist(user.Id, type, listId, itemId, language, country);
            
            await HandlePlaybackState(user, type, listId, playlistResult.Item1, playlistResult.Item2);
        }
        catch (ArgumentException ex)
        {
            Logger.App($"Invalid playlist type: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.App($"Error in StartPlaybackCommand");
            Logger.App(ex);
        }
    }

    private async Task HandlePlaybackState(User user, string type, dynamic listId, VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist)
    {
        VideoPlayerState? playerState = _videoPlayerStateManager.GetState(user.Id);

        if (playerState is null || playerState.CurrentItem is null || playerState.Playlist.Count == 0)
            await HandleNewPlayerState(user, type, listId, item, playlist);
        else if (IsCurrentPlaylist(playerState, type, listId, item.Id))
            await HandleExistingPlaylistState(user, playerState);
        else
            await HandlePlaylistChange(user, playerState, type, listId, item, playlist);
    }

    private async Task HandleNewPlayerState(User user, string type, dynamic listId, VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist)
    {
        Device device = GetCurrentDevice(user);
        VideoPlayerState videoPlayerState = await VideoPlayerStateFactory.Create(_contextFactory, user, device, item, playlist, type, listId);

        _videoPlayerStateManager.UpdateState(user.Id, videoPlayerState);
        _videoPlaybackService.StartPlaybackTimer(user);
        await _videoPlaybackService.UpdatePlaybackState(user, videoPlayerState);
        await _videoPlaybackService.PublishStartedEventAsync(user.Id, videoPlayerState);
    }

    private Device GetCurrentDevice(User user)
    {
        if (CurrentDevice.TryGetValue(user.Id, out Device? device))
            return device;

        device = ConnectedClients.Clients
            .FirstOrDefault(d => d.Key == Context.ConnectionId).Value;
        CurrentDevice[user.Id] = device;

        return device;
    }

    private static bool IsCurrentPlaylist(VideoPlayerState state, string type, dynamic listId, int itemId)
    {
        return state.CurrentItem is not null && state.CurrentList.ToString().Contains($"{type}/{listId}") &&
               state.CurrentItem?.Id == itemId;
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
        state.Actions.Disallows.Previous = state.CurrentItem is null || state.Playlist.IndexOf(state.CurrentItem) == 0;
        state.Actions.Disallows.Next = state.CurrentItem is null || state.Playlist.IndexOf(state.CurrentItem) == state.Playlist.Count - 1;

        _videoPlaybackService.StartPlaybackTimer(user);
        UpdateDeviceInfo(state);
        await _videoPlaybackService.UpdatePlaybackState(user, state);
        await _videoPlaybackService.PublishStartedEventAsync(user.Id, state);
    }

    private async Task HandlePlaylistChange(User user, VideoPlayerState state, string type, dynamic listId,
        VideoPlaylistResponseDto item, List<VideoPlaylistResponseDto> playlist)
    {
        UpdateDeviceInfo(state);
        UpdatePlaylistInfo(state, type, listId, item, playlist);

        _videoPlaybackService.StartPlaybackTimer(user);
        await _videoPlaybackService.UpdatePlaybackState(user, state);
        await _videoPlaybackService.PublishStartedEventAsync(user.Id, state);
    }

    private void UpdateDeviceInfo(VideoPlayerState state)
    {
        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device)) return;
        state.DeviceId = device.DeviceId;
        state.VolumePercentage = device.VolumePercent;
    }

    private void UpdatePlaylistInfo(VideoPlayerState state, string type, dynamic listId, VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist)
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
                Next = playlist.IndexOf(item) == playlist.Count - 1
            }
        };
    }

    public VideoPlayerState? GetStateCommand()
    {
        User? user = Context.User.User();
        if (user is null) return null;

        _videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? playerState);
        if (playerState is null) return null;

        return playerState;
    }

    public async Task PlaybackCommand(string command, object? data = null)
    {
        User? user = Context.User.User();
        if (user is null) return;

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
                state.VolumePercentage = device.VolumePercent;
            }

        await _videoPlaybackService.UpdatePlaybackState(user, state);
    }

    public async Task ChangeDeviceCommand(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null) return;

        List<Device> connectedDevices = Devices();

        await _clientMessenger.SendTo("ConnectedDevicesState", "videoHub", user.Id, connectedDevices);

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
                        DeviceId = deviceId
                    }
                }
            ]
        };

        await _clientMessenger.SendTo("ChangeDevice", "videoHub", user.Id, payload);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        User? user = Context.User.User();
        if (user == null) return;

        bool stopPlayback = false;

        if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            if (_videoPlayerStateManager.TryGetValue(user.Id, out VideoPlayerState? state))
                if (state.DeviceId == client.DeviceId)
                {
                    _videoPlaybackService.RemoveTimer(user.Id);

                    _videoDeviceManager.RemoveUserDevice(user.Id);

                    stopPlayback = true;
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
                        Stopping = true
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
                        Pausing = !playerState.PlayState,
                        Resuming = playerState.PlayState,
                        Stopping = true,
                        Seeking = true,
                        Muting = true,
                        Previous = playerState.CurrentItem is null || playerState.Playlist.IndexOf(playerState.CurrentItem) == 0,
                        Next = playerState.CurrentItem is null || playerState.Playlist.IndexOf(playerState.CurrentItem) == playerState.Playlist.Count - 1
                    }
                };
            }
        }

        await _videoPlaybackService.UpdatePlaybackState(user, playerState);

        Logger.Socket("Video client disconnected");
    }
}