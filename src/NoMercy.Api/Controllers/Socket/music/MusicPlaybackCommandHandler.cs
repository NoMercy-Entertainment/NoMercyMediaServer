using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.music;

public class MusicPlaybackCommandHandler(MusicPlaybackService musicPlaybackService)
{
    private readonly string[] _repeatStates = ["off", "one", "all"];

    public void HandleCommand(User user, string command, object? data, MusicPlayerState state)
    {
        switch (command.ToLower())
        {
            case "play":
                if(state.Actions.Disallows.Resuming) break;
                HandlePlay(user, state);
                break;
            case "pause":
                if(state.Actions.Disallows.Pausing) break;
                HandlePause(user, state);
                break;
            case "seek":
                if(state.Actions.Disallows.Seeking) break;
                HandleSeek(state, data);
                break;
            case "next":
                if(state.Actions.Disallows.Next) break;
                HandleNext(user, state);
                break;
            case "previous":
                if(state.Actions.Disallows.Previous) break;
                HandlePrevious(user, state);
                break;
            case "stop":
                if(state.Actions.Disallows.Stopping) break;
                HandleStop(state);
                break;
            case "mute":
                if(state.Actions.Disallows.Muting) break;
                state.Muted = !state.Muted;
                break;
            case "shuffle":
                if(state.Actions.Disallows.TogglingShuffle) break;
                state.Shuffle = !state.Shuffle;
                break;
            case "repeat":
                if(state.Actions.Disallows.TogglingRepeatContext) break;
                HandleRepeat(state);
                break;
        }
    }

    private void HandlePlay(User user, MusicPlayerState state)
    {
        state.PlayState = true;
        musicPlaybackService.StartPlaybackTimer(user);
    }

    private void HandlePause(User user, MusicPlayerState state)
    {
        state.PlayState = false;
        musicPlaybackService.RemoveTimer(user.Id);
    }

    private void HandleSeek(MusicPlayerState state, object? data)
    {
        int seekTime = int.Parse(data?.ToString() ?? "0") * 1000;
        state.Time = seekTime;
    }

    private void HandleNext(User user, MusicPlayerState state)
    {
        if (state.CurrentItem == null) return;
        musicPlaybackService.RemoveTimer(user.Id);

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

        musicPlaybackService.StartPlaybackTimer(user);
    }

    private void HandlePlaylistCompletion(User user, MusicPlayerState state)
    {
        switch (state.Repeat)
        {
            case "one":
                // If repeat one, play the same item again
                state.Time = 0;
                musicPlaybackService.StartPlaybackTimer(user);
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
                    musicPlaybackService.StartPlaybackTimer(user);
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

    private void HandlePrevious(User user, MusicPlayerState state)
    {
        if (state.CurrentItem == null) return;

        if (state.Time >= 3000)
        {
            state.Time = 0;
            return;
        }

        musicPlaybackService.RemoveTimer(user.Id);

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

        musicPlaybackService.StartPlaybackTimer(user);
    }

    private void HandleRepeat(MusicPlayerState state)
    {
        int currentIndex = Array.IndexOf(_repeatStates, state.Repeat);
        state.Repeat = _repeatStates[(currentIndex + 1) % _repeatStates.Length];
    }

    private void HandleStop(MusicPlayerState state)
    {
        state.DeviceId = null;
        state.CurrentItem = null;
        state.PlayState = false;
        state.Time = 0;
        state.Backlog = [];
        state.Playlist = [];
        state.CurrentList = new("", UriKind.Relative);
        state.Actions = new()
        {
            Disallows = new()
            {
                Previous = true,
                Resuming = true,
                Pausing = true,
                Muting = true,
                Next = true,
                Seeking = true,
                Stopping = true
            }
        };
    }
}