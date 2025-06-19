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

        // Add current item to backlog
        state.Backlog.Add(state.CurrentItem);

        // Move to the next track
        if (state.Playlist.Count > 0)
        {
            state.CurrentItem = state.Playlist.First();
            state.Playlist.RemoveAt(0);
            state.Time = 0;
        }
        else
        {
            HandlePlaylistCompletion(user, state);
            return;
        }

        playbackService.StartPlaybackTimer(user);
    }

    private void HandlePlaylistCompletion(User user, PlayerState state)
    {
        switch (state.Repeat)
        {
            case "one":
                // If repeat one, play the same item again
                state.Time = 0;
                playbackService.StartPlaybackTimer(user);
                break;
            case "all":
                // If repeat all, move the backlog to the playlist and start from the beginning
                state.Playlist = [.. state.Backlog];
                state.Backlog.Clear();
                if (state.Playlist.Count > 0)
                {
                    state.CurrentItem = state.Playlist.First();
                    state.Playlist.RemoveAt(0);
                    state.Time = 0;
                    state.PlayState = true;
                    playbackService.StartPlaybackTimer(user);
                }
                else
                {
                    // If the playlist is empty, stop playback
                    state.PlayState = false;
                    state.Time = 0;
                    state.CurrentItem = null;
                }

                break;
            default:
                // If repeat is off, stop playback
                state.PlayState = false;
                state.Time = 0;
                state.CurrentItem = null;
                break;
        }
    }

    private void HandlePrevious(User user, PlayerState state)
    {
        if (state.CurrentItem == null) return;

        if (state.Time >= 3000)
        {
            state.Time = 0;
            return;
        }

        playbackService.RemoveTimer(user.Id);

        // Move current item to playlist
        state.Playlist.Insert(0, state.CurrentItem);

        // Move last backlog item to current
        if (state.Backlog.Count > 0)
        {
            state.CurrentItem = state.Backlog.Last();
            state.Backlog.RemoveAt(state.Backlog.Count - 1);
            state.Time = 0;
        }
        else
        {
            // If backlog is empty, stop or go to the start of the playlist
            state.Playlist.RemoveAt(0);
            state.PlayState = false;
            state.Time = 0;
            state.CurrentItem = null;
        }

        playbackService.StartPlaybackTimer(user);
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
                Pausing = true
            }
        };
    }
}