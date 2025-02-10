
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(KeywordId), nameof(MovieId))]
[Index(nameof(KeywordId))]
[Index(nameof(MovieId))]
public class KeywordMovie
{
    [JsonProperty("keyword_id")] public int KeywordId { get; set; }
    public Keyword Keyword { get; set; } = null!;

    [JsonProperty("movie_id")] public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public KeywordMovie()
    {
        //
    }
}
