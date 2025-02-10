
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(GenreId), nameof(TvId))]
[Index(nameof(GenreId))]
[Index(nameof(TvId))]
public class GenreTv
{
    [JsonProperty("genre_id")] public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    public GenreTv()
    {
        //
    }
}
