
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(MovieId), nameof(UserId))]
[Index(nameof(MovieId))]
[Index(nameof(UserId))]
public class MovieUser
{
    [JsonProperty("movie_id")] public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public MovieUser()
    {
        //
    }

    public MovieUser(int movieId, Guid userId)
    {
        MovieId = movieId;
        UserId = userId;
    }
}
