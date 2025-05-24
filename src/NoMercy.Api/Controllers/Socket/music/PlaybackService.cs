using System.Collections.Concurrent;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.Socket.music;

public class PlaybackService
{
    private readonly PlayerStateManager _stateManager;
    private readonly string[] _repeatStates = ["off", "one", "all"];
    private static int _playerStateEventId;
    private static Int32 PlayerStateEventId =>  ++_playerStateEventId;

    public PlaybackService(PlayerStateManager stateManager)
    {
        _stateManager = stateManager;
    }
    
    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private const int TimerInterval = 100;

    internal void StartPlaybackTimer(User user)
    {
        if (_timers.TryGetValue(user.Id, out Timer? existingTimer))
        {
            existingTimer.Dispose();
        }

        if (!_stateManager.TryGetValue(user.Id, out PlayerState? _)) return;

        Timer timer = new(_ =>
        {
            if (!_stateManager.TryGetValue(user.Id, out PlayerState? playerState)) return;
            if (!playerState.PlayState || playerState.CurrentItem is null) return;

            playerState.Time += TimerInterval;
            
            int duration = playerState.CurrentItem.Duration.ToMilliSeconds();
    
            // Logger.App($"{playerState.Time}-{duration}");
            if (playerState.Time >= duration)
            {
                HandleTrackCompletion(user, playerState).Wait();
            }
        }, null, 100, TimerInterval);

        _timers[user.Id] = timer;
    }
    
    public void RemoveTimer(Guid userId) {
        if (_timers.TryRemove(userId, out Timer? timer))
        {
            timer.Dispose();
        }
    }

    public async Task HandleTrackCompletion(User user, PlayerState state)
    {
        if (state.CurrentItem == null) return;
        RemoveTimer(user.Id);

        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
        UpdateStateBasedOnRepeatMode(state, currentIndex);

        await UpdatePlaybackState(user, state);
        StartPlaybackTimer(user);
    }

    public async Task UpdatePlaybackState(User user, PlayerState? state)
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

    private void UpdateStateBasedOnRepeatMode(PlayerState state, int currentIndex)
    {
        state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        switch (state.Repeat)
        {
            case "one":
                state.Time = 0;
                break;
            case "all" when currentIndex == state.Playlist.Count - 1:
                state.Time = 0;
                state.CurrentItem = state.Playlist.FirstOrDefault();
                break;
            default:
                HandleDefaultRepeatMode(state, currentIndex);
                break;
        }
    }

    private static void HandleDefaultRepeatMode(PlayerState state, int currentIndex)
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

}