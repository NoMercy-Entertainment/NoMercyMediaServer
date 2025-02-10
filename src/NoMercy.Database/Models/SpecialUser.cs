
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(SpecialId), nameof(UserId))]
public class SpecialUser
{
    [JsonProperty("special_id")] public Ulid SpecialId { get; set; }
    public Special Special { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public SpecialUser()
    {
        //
    }

    public SpecialUser(Ulid specialId, Guid userId)
    {
        SpecialId = specialId;
        UserId = userId;
    }
}
