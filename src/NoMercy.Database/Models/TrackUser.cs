
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(TrackId), nameof(UserId))]
[Index(nameof(TrackId))]
[Index(nameof(UserId))]
public class TrackUser
{
    [JsonProperty("track_id")] public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public TrackUser()
    {
        //
    }

    public TrackUser(Guid trackId, Guid userId)
    {
        TrackId = trackId;
        UserId = userId;
    }
}
