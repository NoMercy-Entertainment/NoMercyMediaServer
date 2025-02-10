
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(LibraryId), nameof(TvId))]
[Index(nameof(LibraryId))]
[Index(nameof(TvId))]
public class LibraryTv
{
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    public LibraryTv()
    {
        //
    }

    public LibraryTv(Ulid libraryId, int tvId)
    {
        LibraryId = libraryId;
        TvId = tvId;
    }
}
