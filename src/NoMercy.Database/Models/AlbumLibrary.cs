using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(AlbumId), nameof(LibraryId))]
[Index(nameof(AlbumId))]
[Index(nameof(LibraryId))]
public class AlbumLibrary
{
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public AlbumLibrary()
    {
    }

    public AlbumLibrary(Guid albumId, Ulid libraryId)
    {
        AlbumId = albumId;
        LibraryId = libraryId;
    }
}
