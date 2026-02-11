using NoMercy.Api.DTOs.Music;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Services.Music;

public class MusicPlayerStateFactory
{
    public static MusicPlayerState Create(
        Device device,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist,
        string type,
        Guid listId)
    {
        return new()
        {
            DeviceId = device.DeviceId,
            VolumePercentage = device.VolumePercent,
            CurrentItem = item,
            Backlog = [item],
            Playlist = playlist,
            PlayState = true,
            Time = 0,
            Duration = item.Duration.ToMilliSeconds(),
            Shuffle = false,
            Repeat = "off",
            CurrentList = new($"/music/{type}/{listId}", UriKind.Relative),
            Actions = new()
            {
                Disallows = new()
                {
                    // Can't pause when starting (we're playing)
                    Pausing = false,
                    // Can't resume when already playing
                    Resuming = true,
                    // Can't go to previous if backlog only has current item
                    Previous = true,
                    // Can't go to next if playlist is empty after current
                    Next = playlist.Count <= 0,
                    // Basic actions that are always allowed
                    Seeking = false,
                    Stopping = false,
                    Muting = false,
                    TogglingShuffle = false,
                    TogglingRepeatContext = false,
                    TogglingRepeatTrack = false
                }
            }
        };
    }
}