using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(AlbumId), nameof(MusicGenreId))]
[Index(nameof(AlbumId))]
[Index(nameof(MusicGenreId))]
public class AlbumMusicGenre
{
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("music_genre_id")] public Guid MusicGenreId { get; set; }
    public MusicGenre MusicGenre { get; set; } = null!;

    public AlbumMusicGenre()
    {
    }

    public AlbumMusicGenre(Guid albumId, Guid musicGenreId)
    {
        AlbumId = albumId;
        MusicGenreId = musicGenreId;
    }
}
