using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(AlbumId), nameof(TrackId))]
[Index(nameof(AlbumId))]
[Index(nameof(TrackId))]
public class AlbumTrack
{
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("track_id")] public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public AlbumTrack()
    {
    }

    public AlbumTrack(Guid albumId, Guid trackId)
    {
        AlbumId = albumId;
        TrackId = trackId;
    }
}
