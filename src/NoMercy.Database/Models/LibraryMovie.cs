
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(LibraryId), nameof(MovieId))]
[Index(nameof(LibraryId))]
[Index(nameof(MovieId))]
public class LibraryMovie
{
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty("movie_id")] public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public LibraryMovie()
    {
        //
    }

    public LibraryMovie(Ulid libraryId, int movieId)
    {
        LibraryId = libraryId;
        MovieId = movieId;
    }
}
