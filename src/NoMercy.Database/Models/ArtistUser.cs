
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(ArtistId), nameof(UserId))]
[Index(nameof(ArtistId))]
[Index(nameof(UserId))]
public class ArtistUser
{
    [JsonProperty("artist_id")] public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ArtistUser()
    {
    }

    public ArtistUser(Guid artistId, Guid userId)
    {
        ArtistId = artistId;
        UserId = userId;
    }
}
