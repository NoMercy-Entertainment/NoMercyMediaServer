using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.Socket.music;

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
                    Previous = false,
                    Resuming = false
                }
            }
        };
    }
}