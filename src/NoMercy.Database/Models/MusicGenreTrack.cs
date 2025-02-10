
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(GenreId), nameof(TrackId))]
[Index(nameof(GenreId))]
[Index(nameof(TrackId))]
public class MusicGenreTrack
{
    [JsonProperty("genre_id")] public Guid GenreId { get; set; }
    public MusicGenre Genre { get; set; } = null!;

    [JsonProperty("track_id")] public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public MusicGenreTrack()
    {
        //
    }

    public MusicGenreTrack(Guid genreId, Guid trackId)
    {
        GenreId = genreId;
        TrackId = trackId;
    }
}
