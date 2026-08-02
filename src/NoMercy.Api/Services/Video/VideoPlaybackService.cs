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
using NoMercy.Events.Library;
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
    private static int PlayerStateEventId => Interlocked.Increment(ref field);

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

    /// <summary>
    /// When each user last had the continue-watching carousel invalidated, and on which item.
    /// </summary>
    private readonly ConcurrentDictionary<
        Guid,
        (string Item, DateTime At)
    > _lastContinueWatchingRefresh = new();

    /// <summary>
    /// How long the carousel is left alone between progress reports for the same item. Progress
    /// arrives about once a second per playing device and every report reached every connected
    /// client as a cache invalidation, so each one refetched the row once a second for the whole
    /// length of a film. The row shows a resume position, not a running clock; it does not need
    /// to be correct to the second on a device nobody is watching.
    /// </summary>
    private static readonly TimeSpan ContinueWatchingRefreshInterval = TimeSpan.FromSeconds(30);
    private const int TimerInterval = 100;

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(user.Id, out Timer? existingTimer))
            existingTimer.Dispose();

        if (!_stateManager.TryGetValue(user.Id, out VideoPlayerState? _))
            return;

        Timer timer = new(
            async _ =>
            {
                // A throw from this async-void timer callback is rethrown on the thread
                // pool and terminates the whole server, so nothing may escape here.
                try
                {
                    if (!_stateManager.TryGetValue(user.Id, out VideoPlayerState? playerState))
                        return;
                    if (!playerState.PlayState || playerState.CurrentItem is null)
                        return;

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

                    if (playerState.Time < duration - TimerInterval)
                        return;

                    RemoveTimer(user.Id);
                    await HandleTrackCompletion(user, playerState);
                }
                catch (Exception ex)
                {
                    Logger.App(
                        $"Playback timer tick failed for user {user.Id}: {ex.Message}",
                        LogEventLevel.Error
                    );
                }
            },
            null,
            100,
            TimerInterval
        );

        _timers[user.Id] = timer;
    }

    public void RemoveTimer(Guid userId)
    {
        if (_timers.TryRemove(userId, out Timer? timer))
            timer.Dispose();
    }

    private async Task HandleTrackCompletion(User user, VideoPlayerState state)
    {
        if (state.CurrentItem == null)
            return;
        RemoveTimer(user.Id);

        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);

        if (currentIndex + 1 == state.Playlist.Count)
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

        await _clientMessenger.SendTo("VideoPlayerState", "videoHub", user.Id, payload);
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
                    Stopping = true,
                },
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
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            new PlaybackStartedEvent
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
            new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = state.CurrentItem.TmdbId,
                Position = TimeSpan.FromMilliseconds(state.Time),
                Duration = TimeSpan.FromMilliseconds(duration),
            }
        );
    }

    /// <summary>
    /// Tells clients the continue-watching carousel is stale. Watch progress is
    /// exactly what that row is built from, so without this the carousel only
    /// caught up on a full page load: a title started on one device kept showing
    /// its old position (or stayed missing entirely) on every other device.
    /// Mirrors the key TvShowsController already publishes after a watch toggle.
    /// </summary>
    private async Task PublishContinueWatchingRefreshAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null)
            return;

        if (!ShouldRefreshContinueWatching(userId, state))
            return;

        await bus.PublishAsync(new LibraryRefreshedEvent { QueryKey = ["continue-watching"] });
    }

    /// <summary>
    /// True when the carousel has actually gone stale: a different title is being watched, or
    /// enough time has passed that the stored resume position is worth catching up on.
    /// </summary>
    private bool ShouldRefreshContinueWatching(Guid userId, VideoPlayerState state)
    {
        string item =
            $"{state.CurrentItem?.PlaylistType}:{state.CurrentItem?.TmdbId}:{state.CurrentItem?.VideoId}";
        DateTime now = DateTime.UtcNow;

        if (!_lastContinueWatchingRefresh.TryGetValue(userId, out (string Item, DateTime At) last))
        {
            _lastContinueWatchingRefresh[userId] = (item, now);
            return true;
        }

        bool itemChanged = !string.Equals(last.Item, item, StringComparison.Ordinal);
        bool intervalElapsed = now - last.At >= ContinueWatchingRefreshInterval;

        if (!itemChanged && !intervalElapsed)
            return false;

        _lastContinueWatchingRefresh[userId] = (item, now);
        return true;
    }

    private async Task PublishCompletedEventAsync(Guid userId, VideoPlayerState state)
    {
        IEventBus? bus =
            _eventBus ?? (EventBusProvider.IsConfigured ? EventBusProvider.Current : null);
        if (bus is null || state.CurrentItem is null)
            return;

        await bus.PublishAsync(
            new PlaybackCompletedEvent
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
                $"StoreWatchProgression: unsupported playlist type '{state.CurrentItem.PlaylistType}', skipping",
                LogEventLevel.Warning
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
        UpsertCommandBuilder<UserData> query = mediaContext.UserData.Upsert(userdata);

        query = state.CurrentItem.PlaylistType switch
        {
            MediaTypes.MovieMediaType => query.On(x => new
            {
                x.VideoFileId,
                x.UserId,
                x.MovieId,
            }),
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => query.On(x => new
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
            _ => throw new ArgumentException(
                "Invalid playlist type",
                state.CurrentItem.PlaylistType
            ),
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
                        RemovedFromContinueWatching = false,
                    }
            )
            .RunAsync();

        await PublishContinueWatchingRefreshAsync(user.Id, state);
    }
}
