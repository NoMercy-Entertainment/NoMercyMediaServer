
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(LibraryId), nameof(TrackId))]
[Index(nameof(LibraryId))]
[Index(nameof(TrackId))]
public class LibraryTrack
{
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty("track_id")] public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public LibraryTrack()
    {
        //
    }

    public LibraryTrack(Ulid libraryId, Guid trackId)
    {
        LibraryId = libraryId;
        TrackId = trackId;
    }
}
