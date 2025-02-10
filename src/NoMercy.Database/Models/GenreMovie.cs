
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(GenreId), nameof(MovieId))]
[Index(nameof(GenreId))]
[Index(nameof(MovieId))]
public class GenreMovie
{
    [JsonProperty("genre_id")] public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    [JsonProperty("movie_id")] public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public GenreMovie()
    {
        //
    }
}
