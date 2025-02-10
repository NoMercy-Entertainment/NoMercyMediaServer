using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(AlbumId), nameof(UserId))]
[Index(nameof(AlbumId))]
[Index(nameof(UserId))]
public class AlbumUser
{
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public AlbumUser()
    {
    }

    public AlbumUser(Guid albumId, Guid userId)
    {
        AlbumId = albumId;
        UserId = userId;
    }
}
