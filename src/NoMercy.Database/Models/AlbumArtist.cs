using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(AlbumId), nameof(ArtistId))]
[Index(nameof(AlbumId))]
[Index(nameof(ArtistId))]
public class AlbumArtist
{
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("artist_id")] public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    public AlbumArtist()
    {
    }

    public AlbumArtist(Guid albumId, Guid artistId)
    {
        AlbumId = albumId;
        ArtistId = artistId;
    }
}
