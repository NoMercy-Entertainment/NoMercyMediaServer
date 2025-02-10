
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(ArtistId), nameof(LibraryId))]
[Index(nameof(ArtistId))]
[Index(nameof(LibraryId))]
public class ArtistLibrary
{
    [JsonProperty("artist_id")] public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public ArtistLibrary()
    {
    }

    public ArtistLibrary(Guid artistId, Ulid libraryId)
    {
        ArtistId = artistId;
        LibraryId = libraryId;
    }
}
