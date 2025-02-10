
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(TvId), nameof(UserId))]
[Index(nameof(TvId))]
[Index(nameof(UserId))]
public class TvUser
{
    [JsonProperty("tv_id")] public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public TvUser()
    {
        //
    }

    public TvUser(int tvId, Guid userId)
    {
        TvId = tvId;
        UserId = userId;
    }
}
