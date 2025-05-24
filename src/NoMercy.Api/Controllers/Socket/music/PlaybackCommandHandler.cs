using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.music;

public class PlaybackCommandHandler(PlaybackService playbackService)
{
    private readonly string[] _repeatStates = ["off", "one", "all"];
    
    public void HandleCommand(User user, string command, object? data, PlayerState state)
    {
        switch (command.ToLower())
        {
            case "play":
                HandlePlay(user, state);
                break;
            case "pause":
                HandlePause(user, state);
                break;
            case "seek":
                HandleSeek(state, data);
                break;
            case "next":
                HandleNext(user, state);
                break;
            case "previous":
                HandlePrevious(user, state);
                break;
            case "shuffle":
                state.Shuffle = !state.Shuffle;
                break;
            case "repeat":
                HandleRepeat(state);
                break;
            case "stop":
                HandleStop(state);
                break;
            case "mute":
                state.Muted = !state.Muted;
                break;
        }

        state.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void HandlePlay(User user, PlayerState state)
    {
        state.PlayState = true;
        playbackService.StartPlaybackTimer(user);
    }
    private void HandlePause(User user, PlayerState state)
    {
        state.PlayState = false;
        playbackService.RemoveTimer(user.Id);
    }
    private void HandleSeek(PlayerState state, object? data)
    {
        int seekTime = int.Parse(data?.ToString() ?? "0") * 1000;
        state.Time = seekTime;
    }
    private void HandleNext(User user, PlayerState state)
    {
        if (state.CurrentItem == null) return;
        playbackService.RemoveTimer(user.Id);
        
        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
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
                if (currentIndex + 1 < state.Playlist.Count)
                {
                    state.PlayState = true;
                    state.Time = 0;
                    state.CurrentItem = state.Playlist[currentIndex + 1];
                    // Task.Delay(100).Wait();
                }
                else
                {
                    state.PlayState = false;
                    state.Time = 0;
                    state.CurrentItem = null;
                }
                break;
        }
        playbackService.StartPlaybackTimer(user);
    }
    private void HandlePrevious(User user, PlayerState state)
    {
        if (state.CurrentItem == null) return;

        if (state.Time >= 3000)
        {
            state.Time = 0;
            return;
        }
        
        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
        if (currentIndex <= 0) return;
        state.CurrentItem = state.Playlist[currentIndex - 1];
        state.Time = 0;
    }
    private void HandleRepeat(PlayerState state)
    {
        int currentIndex = Array.IndexOf(_repeatStates, state.Repeat);
        state.Repeat = _repeatStates[(currentIndex + 1) % _repeatStates.Length];
    }
    private void HandleStop(PlayerState state)
    {
        state.DeviceId = null;
        state.CurrentItem = null;
        state.PlayState = false;
        state.Time = 0;
        state.Playlist = [];
        state.CurrentList = "";
        state.Actions = new()
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