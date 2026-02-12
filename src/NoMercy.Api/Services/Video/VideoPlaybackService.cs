using System.Collections.Concurrent;
using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Playback;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Services.Video;

public class VideoPlaybackService
{
    private readonly VideoPlayerStateManager _stateManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus? _eventBus;
    private static int _playerStateEventId;
    private static int PlayerStateEventId => ++_playerStateEventId;

    public VideoPlaybackService(VideoPlayerStateManager stateManager, IServiceScopeFactory scopeFactory, IEventBus? eventBus = null)
    {
        _stateManager = stateManager;
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
    }

    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private readonly ConcurrentDictionary<Guid, int> _lastTimes = new();
    private const int TimerInterval = 100;

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(user.Id, out Timer? existingTimer)) existingTimer.Dispose();

        if (!_stateManager.TryGetValue(user.Id, out VideoPlayerState? _)) return;

        Timer timer = new(async _ =>
        {
            if (!_stateManager.TryGetValue(user.Id, out VideoPlayerState? playerState)) return;
            if (!playerState.PlayState || playerState.CurrentItem is null) return;

            playerState.Time += TimerInterval;

            if (_lastTimes.TryGetValue(user.Id, out int lastTimer) && lastTimer >= 1000)
            {
                _lastTimes[user.Id] = 0;
                await StoreWatchProgression(playerState, user);
                await PublishProgressEventAsync(user.Id, playerState);
            }
            else
            {
                _lastTimes.AddOrUpdate(user.Id, 0, (_, value) => value + TimerInterval);
            }

            int duration = playerState.CurrentItem.Duration.ToMilliSeconds();

            // Logger.App($"{playerState.Time}-{duration}");
            if (playerState.Time < duration - TimerInterval) return;

            RemoveTimer(user.Id);
            await HandleTrackCompletion(user, playerState);
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

        if(currentIndex + 1 == state.Playlist.Count)
        {
            await PublishCompletedEventAsync(user.Id, state);

            UpdateState(state, -1);

            await UpdatePlaybackState(user, state);

            _stateManager.RemoveState(user.Id);

            return;
        }

        UpdateState(state, currentIndex + 1);

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
        if (currentIndex == -1)
        {
            state.PlayState = true;
            state.Time = 0;
            state.CurrentItem = null;
            state.Playlist.Clear();
            state.CurrentList = new("/home", UriKind.Relative);
            state.Actions = new()
            {
                Disallows = new()
                {
                    Next = true,
                    Previous = true,
                    Muting = true,
                    Pausing = true,
                    Resuming = true,
                    Seeking = true,
                    Stopping = true
                }
            };
        }
        else if (currentIndex + 1 < state.Playlist.Count)
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
    
    internal async Task PublishStartedEventAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null) return;

        await bus.PublishAsync(new PlaybackStartedEvent
        {
            UserId = userId,
            MediaId = state.CurrentItem.TmdbId,
            MediaType = state.CurrentItem.PlaylistType,
            DeviceId = state.DeviceId
        });
    }

    private async Task PublishProgressEventAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null) return;

        int duration = state.CurrentItem.Duration.ToMilliSeconds();

        await bus.PublishAsync(new PlaybackProgressEvent
        {
            UserId = userId,
            MediaId = state.CurrentItem.TmdbId,
            Position = TimeSpan.FromMilliseconds(state.Time),
            Duration = TimeSpan.FromMilliseconds(duration)
        });
    }

    private async Task PublishCompletedEventAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus = _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null) return;

        await bus.PublishAsync(new PlaybackCompletedEvent
        {
            UserId = userId,
            MediaId = state.CurrentItem.TmdbId,
            MediaType = state.CurrentItem.PlaylistType
        });
    }

    internal async Task StoreWatchProgression(VideoPlayerState state, User user)
    {
        if (state.CurrentItem is null || state.Time <= 0) return;

        UserData userdata = new()
        {
            UserId = user.Id,
            Type = state.CurrentItem.PlaylistType,
            Time = state.Time / 1000,
            VideoFileId = state.CurrentItem.VideoId,
            MovieId = state.CurrentItem.PlaylistType == Config.MovieMediaType
                ? state.CurrentItem.TmdbId
                : null,
            TvId = state.CurrentItem.PlaylistType == Config.TvMediaType
                ? state.CurrentItem.TmdbId
                : null,
            CollectionId = state.CurrentItem.PlaylistType == Config.CollectionMediaType
                ? int.Parse(state.CurrentItem.PlaylistId)
                : null,
            SpecialId = state.CurrentItem.PlaylistType == Config.SpecialMediaType
                ? Ulid.Parse(state.CurrentItem.PlaylistId)
                : null
        };

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync();
        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(userdata);
        
        query = state.CurrentItem.PlaylistType switch
        {
            Config.MovieMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.MovieId }),
            Config.TvMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.TvId }),
            Config.CollectionMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.CollectionId }),
            Config.SpecialMediaType => query.On(x => new { x.VideoFileId, x.UserId, x.SpecialId }),
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
                LastPlayedDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
            .RunAsync();
    }
}