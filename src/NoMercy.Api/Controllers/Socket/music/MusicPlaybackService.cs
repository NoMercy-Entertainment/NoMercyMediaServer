using System.Collections.Concurrent;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.Socket.music;

public class MusicPlaybackService
{
    private readonly MusicPlayerStateManager _stateManager;
    private readonly MusicRepository _musicRepository;
    private readonly string[] _repeatStates = ["off", "one", "all"];
    private static int _playerStateEventId;
    private static int PlayerStateEventId => ++_playerStateEventId;

    public MusicPlaybackService(MusicPlayerStateManager stateManager)
    {
        _stateManager = stateManager;
        _musicRepository = new(new());
    }

    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private const int TimerInterval = 100;

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(user.Id, out Timer? existingTimer)) existingTimer.Dispose();

        if (!_stateManager.TryGetValue(user.Id, out MusicPlayerState? _)) return;

        Timer timer = new(async _ =>
        {
            if (!_stateManager.TryGetValue(user.Id, out MusicPlayerState? playerState)) return;
            if (!playerState.PlayState || playerState.CurrentItem is null) return;

            playerState.Time += TimerInterval;

            int duration = playerState.CurrentItem.Duration.ToMilliSeconds();

            if (playerState.Time >= (duration / 2) && playerState.Time < (duration / 2) + TimerInterval)
            {
                await _musicRepository.RecordPlaybackAsync(playerState.CurrentItem.Id, user.Id);
            }

            if (playerState.Time >= duration) await HandleTrackCompletion(user, playerState);
        }, null, 100, TimerInterval);

        _timers[user.Id] = timer;
    }

    public void RemoveTimer(Guid userId)
    {
        if (_timers.TryRemove(userId, out Timer? timer)) timer.Dispose();
    }

    private async Task HandleTrackCompletion(User user, MusicPlayerState state)
    {
        if (state.CurrentItem == null) return;
        RemoveTimer(user.Id);

        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
        UpdateStateBasedOnRepeatMode(state, currentIndex);

        await UpdatePlaybackState(user, state);
        StartPlaybackTimer(user);
    }

    public async Task UpdatePlaybackState(User user, MusicPlayerState? state)
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
                    Source = "musicHub",
                    Type = MusicEventType.PlayerStateChanged,
                    User = user
                }
            ]
        };

        await Networking.Networking.SendTo("MusicPlayerState", "musicHub", user.Id, payload);
    }

    private void UpdateStateBasedOnRepeatMode(MusicPlayerState state, int currentIndex)
    {
        switch (state.Repeat)
        {
            case "one":
                state.Time = 0;
                break;
            case "all":
                if (currentIndex == state.Playlist.Count - 1)
                {
                    // Move the current item to the backlog
                    if (state.CurrentItem != null) state.Backlog.Add(state.CurrentItem);

                    // Move the backlog to the playlist and start from the beginning
                    state.Playlist = [.. state.Backlog];
                    state.Backlog.Clear();

                    if (state.Playlist.Count > 0)
                    {
                        state.CurrentItem = state.Playlist.First();
                        state.Playlist.RemoveAt(0);
                        state.Time = 0;
                        state.PlayState = true;
                    }
                    else
                    {
                        // If the playlist is empty, stop playback
                        state.PlayState = false;
                        state.Time = 0;
                        state.CurrentItem = null;
                    }
                }
                else
                {
                    if (state.CurrentItem != null) state.Backlog.Add(state.CurrentItem);
                    state.CurrentItem = state.Playlist[currentIndex + 1];
                    state.Playlist.RemoveAt(currentIndex + 1);
                    state.Time = 0;
                }

                break;
            default:
                if (state.CurrentItem != null) state.Backlog.Add(state.CurrentItem);
                if (currentIndex + 1 < state.Playlist.Count)
                {
                    state.PlayState = true;
                    state.Time = 0;
                    state.CurrentItem = state.Playlist[currentIndex + 1];
                    state.Playlist.RemoveAt(currentIndex + 1);
                }
                else
                {
                    state.PlayState = false;
                    state.Time = 0;
                    state.CurrentItem = null;
                }

                break;
        }
    }
}