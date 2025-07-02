using System.Collections.Concurrent;
using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.Socket.video;

public class VideoPlaybackService
{
    private readonly VideoPlayerStateManager _stateManager;
    private static int _playerStateEventId;
    private static int PlayerStateEventId => ++_playerStateEventId;

    public VideoPlaybackService(VideoPlayerStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private readonly ConcurrentDictionary<Guid, int> _lastTimes = new();
    private const int TimerInterval = 100;

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(user.Id, out Timer? existingTimer)) existingTimer.Dispose();

        if (!_stateManager.TryGetValue(user.Id, out VideoPlayerState? _)) return;

        Timer timer = new(_ =>
        {
            if (!_stateManager.TryGetValue(user.Id, out VideoPlayerState? playerState)) return;
            if (!playerState.PlayState || playerState.CurrentItem is null) return;

            playerState.Time += TimerInterval;

            if (_lastTimes.TryGetValue(user.Id, out int lastTimer) && lastTimer >= 1000)
            {
                _lastTimes[user.Id] = 0;
                StoreWatchProgression(playerState, user).Wait();
            }
            else 
            {
                _lastTimes.AddOrUpdate(user.Id, 0, (_, value) => value + TimerInterval);
            }

            int duration = playerState.CurrentItem.Duration.ToMilliSeconds();

            // Logger.App($"{playerState.Time}-{duration}");
            if (playerState.Time >= duration) HandleTrackCompletion(user, playerState).Wait();
        }, null, 100, TimerInterval);

        _timers[user.Id] = timer;
    }

    public void RemoveTimer(Guid userId)
    {
        if (_timers.TryRemove(userId, out Timer? timer)) timer.Dispose();
    }

    private async Task HandleTrackCompletion(User user, VideoPlayerState state)
    {
        if (state.CurrentItem == null) return;
        RemoveTimer(user.Id);

        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
        UpdateState(state, currentIndex);

        await UpdatePlaybackState(user, state);
        StartPlaybackTimer(user);
    }

    public async Task UpdatePlaybackState(User user, VideoPlayerState? state)
    {
        if (state is not null) state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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
                    Source = "videoHub",
                    Type = VideoEventType.PlayerStateChanged,
                    User = user
                }
            ]
        };

        await Networking.Networking.SendTo("VideoPlayerState", "videoHub", user.Id, payload);
    }

    private void UpdateState(VideoPlayerState state, int currentIndex)
    {
        if (currentIndex + 1 < state.Playlist.Count)
        {
            state.PlayState = true;
            state.Time = 0;
            state.CurrentItem = state.Playlist[currentIndex + 1];
        }
        else
        {
            state.PlayState = false;
            state.Time = 0;
            state.CurrentItem = null;
        }
    }
    
    internal static async Task StoreWatchProgression(VideoPlayerState state, User user)
    {
        if (state.CurrentItem is null || state.Time <= 0) return;
        
        UserData userdata = new()
        {
            UserId = user.Id,
            Type = state.CurrentItem.PlaylistType,
            Time = state.Time / 1000,
            VideoFileId = state.CurrentItem.VideoId,
            MovieId = state.CurrentItem.PlaylistType == "movie" 
                ? state.CurrentItem.TmdbId
                : null,
            TvId = state.CurrentItem.PlaylistType == "tv" 
                ? state.CurrentItem.TmdbId
                : null,
            CollectionId = state.CurrentItem.PlaylistType == "collection"
                ? int.Parse(state.CurrentItem.PlaylistId) 
                : null,
            SpecialId = state.CurrentItem.PlaylistType == "specials"
                ? Ulid.Parse(state.CurrentItem.PlaylistId) 
                : null,
        };

        await using MediaContext mediaContext = new();
        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(userdata);
        
        query = state.CurrentItem.PlaylistType switch
        {
            "movie" => query.On(x => new { x.VideoFileId, x.UserId, x.MovieId }),
            "tv" => query.On(x => new { x.VideoFileId, x.UserId, x.TvId }),
            "collection" => query.On(x => new { x.VideoFileId, x.UserId, x.CollectionId }),
            "specials" => query.On(x => new { x.VideoFileId, x.UserId, x.SpecialId }),
            _ => throw new ArgumentException("Invalid playlist type", state.CurrentItem.PlaylistType)
        };
        
        await query
            .WhenMatched((uds, udi) => new()
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
                UpdatedAt = udi.UpdatedAt
            })
            .RunAsync();
    }
}