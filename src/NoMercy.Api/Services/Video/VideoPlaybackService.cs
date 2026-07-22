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
using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Playback;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Services.Video;

public class VideoPlaybackService
{
    private readonly VideoPlayerStateManager _stateManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClientMessenger _clientMessenger;
    private readonly IEventBus? _eventBus;
    private static int PlayerStateEventId => Interlocked.Increment(location: ref field);

    public VideoPlaybackService(
        VideoPlayerStateManager stateManager,
        IServiceScopeFactory scopeFactory,
        IClientMessenger clientMessenger,
        IEventBus? eventBus = null
    )
    {
        _stateManager = stateManager;
        _scopeFactory = scopeFactory;
        _clientMessenger = clientMessenger;
        _eventBus = eventBus;
    }

    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private readonly ConcurrentDictionary<Guid, int> _lastTimes = new();
    private const int TimerInterval = 100;

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(key: user.Id, value: out Timer? existingTimer))
            existingTimer.Dispose();

        if (!_stateManager.TryGetValue(userId: user.Id, state: out VideoPlayerState? _))
            return;

        Timer timer = new(
            callback: async _ =>
            {
                // A throw from this async-void timer callback is rethrown on the thread
                // pool and terminates the whole server, so nothing may escape here.
                try
                {
                    if (!_stateManager.TryGetValue(userId: user.Id, state: out VideoPlayerState? playerState))
                        return;
                    if (!playerState.PlayState || playerState.CurrentItem is null)
                        return;

                    playerState.Time += TimerInterval;

                    if (_lastTimes.TryGetValue(key: user.Id, value: out int lastTimer) && lastTimer >= 1000)
                    {
                        _lastTimes[key: user.Id] = 0;
                        await StoreWatchProgression(state: playerState, user: user);
                        await PublishProgressEventAsync(userId: user.Id, state: playerState);
                    }
                    else
                    {
                        _lastTimes.AddOrUpdate(key: user.Id, addValue: 0, updateValueFactory: (_, value) => value + TimerInterval);
                    }

                    int duration = playerState.CurrentItem.Duration.ToMilliSeconds();

                    if (playerState.Time < duration - TimerInterval)
                        return;

                    RemoveTimer(userId: user.Id);
                    await HandleTrackCompletion(user: user, state: playerState);
                }
                catch (Exception ex)
                {
                    Logger.App(
                        message: $"Playback timer tick failed for user {user.Id}: {ex.Message}",
                        level: LogEventLevel.Error
                    );
                }
            },
            state: null,
            dueTime: 100,
            period: TimerInterval
        );

        _timers[key: user.Id] = timer;
    }

    public void RemoveTimer(Guid userId)
    {
        if (_timers.TryRemove(key: userId, value: out Timer? timer))
            timer.Dispose();
    }

    private async Task HandleTrackCompletion(User user, VideoPlayerState state)
    {
        if (state.CurrentItem == null)
            return;
        RemoveTimer(userId: user.Id);

        int currentIndex = state.Playlist.IndexOf(item: state.CurrentItem);

        if (currentIndex + 1 == state.Playlist.Count)
        {
            await PublishCompletedEventAsync(userId: user.Id, state: state);

            UpdateState(state: state, currentIndex: -1);

            await UpdatePlaybackState(user: user, state: state);

            _stateManager.RemoveState(userId: user.Id);

            return;
        }

        UpdateState(state: state, currentIndex: currentIndex + 1);

        await UpdatePlaybackState(user: user, state: state);

        StartPlaybackTimer(user: user);
    }

    public async Task UpdatePlaybackState(User user, VideoPlayerState? state)
    {
        if (state is not null)
            state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        EventPayload<PlayerStateEventElement> payload = new()
        {
            Events =
            [
                new()
                {
                    Event = new() { EventId = PlayerStateEventId, State = state },
                    Source = "videoHub",
                    Type = VideoEventType.PlayerStateChanged,
                    User = user,
                },
            ],
        };

        await _clientMessenger.SendTo(name: "VideoPlayerState", endpoint: "videoHub", userId: user.Id, data: payload);
    }

    private void UpdateState(VideoPlayerState state, int currentIndex)
    {
        if (currentIndex == -1)
        {
            state.PlayState = true;
            state.Time = 0;
            state.CurrentItem = null;
            state.Playlist.Clear();
            state.CurrentList = new(uriString: "/home", uriKind: UriKind.Relative);
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
                    Stopping = true,
                },
            };
        }
        else if (currentIndex + 1 < state.Playlist.Count)
        {
            state.PlayState = true;
            state.Time = 0;
            state.CurrentItem = state.Playlist[index: currentIndex + 1];
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
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = state.CurrentItem.TmdbId,
                MediaType = state.CurrentItem.PlaylistType,
                DeviceId = state.DeviceId,
            }
        );
    }

    private async Task PublishProgressEventAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        int duration = state.CurrentItem.Duration.ToMilliSeconds();

        await bus.PublishAsync(
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = state.CurrentItem.TmdbId,
                Position = TimeSpan.FromMilliseconds(milliseconds: state.Time),
                Duration = TimeSpan.FromMilliseconds(milliseconds: duration),
            }
        );
    }

    private async Task PublishCompletedEventAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            @event: new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = state.CurrentItem.TmdbId,
                MediaType = state.CurrentItem.PlaylistType,
            }
        );
    }

    internal async Task StoreWatchProgression(VideoPlayerState state, User user)
    {
        if (state.CurrentItem is null || state.Time <= 0)
            return;

        // Only the playable video types persist watch progression. Skip (never throw)
        // for anything else — a stray type reaching the switch below used to throw and,
        // from the async-void playback timer, take the whole server down.
        if (
            state.CurrentItem.PlaylistType
            is not (
                MediaTypes.MovieMediaType
                or MediaTypes.TvMediaType
                or MediaTypes.AnimeMediaType
                or MediaTypes.CollectionMediaType
                or MediaTypes.SpecialMediaType
            )
        )
        {
            Logger.App(
                message: $"StoreWatchProgression: unsupported playlist type '{state.CurrentItem.PlaylistType}', skipping",
                level: LogEventLevel.Warning
            );
            return;
        }

        UserData userdata = new()
        {
            UserId = user.Id,
            Type = state.CurrentItem.PlaylistType,
            Time = state.Time / 1000,
            VideoFileId = state.CurrentItem.VideoId,
            MovieId =
                state.CurrentItem.PlaylistType == MediaTypes.MovieMediaType
                    ? state.CurrentItem.TmdbId
                    : null,
            TvId = state.CurrentItem.PlaylistType
                is MediaTypes.TvMediaType
                    or MediaTypes.AnimeMediaType
                ? state.CurrentItem.TmdbId
                : null,
            CollectionId =
                state.CurrentItem.PlaylistType == MediaTypes.CollectionMediaType
                    ? int.Parse(state.CurrentItem.PlaylistId)
                    : null,
            SpecialId =
                state.CurrentItem.PlaylistType == MediaTypes.SpecialMediaType
                    ? Ulid.Parse(state.CurrentItem.PlaylistId)
                    : null,
        };

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync();
        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(entity: userdata);

        query = state.CurrentItem.PlaylistType switch
        {
            MediaTypes.MovieMediaType => query.On(match: x => new
            {
                x.VideoFileId,
                x.UserId,
                x.MovieId,
            }),
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => query.On(match: x => new
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
            _ => throw new ArgumentException(
                message: "Invalid playlist type",
                paramName: state.CurrentItem.PlaylistType
            ),
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
}
