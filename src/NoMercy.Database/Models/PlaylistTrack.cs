
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(PlaylistId), nameof(TrackId))]
[Index(nameof(PlaylistId))]
[Index(nameof(TrackId))]
public class PlaylistTrack
{
    [JsonProperty("playlist_id")] public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    [JsonProperty("track_id")] public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public PlaylistTrack()
    {
        //
    }

    public PlaylistTrack(Guid playlistId, Guid trackId)
    {
        PlaylistId = playlistId;
        TrackId = trackId;
    }
}
