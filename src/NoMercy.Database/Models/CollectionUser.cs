
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(CollectionId), nameof(UserId))]
[Index(nameof(CollectionId))]
[Index(nameof(UserId))]
public class CollectionUser
{
    [JsonProperty("collection_id")] public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
 
    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public CollectionUser()
    {
    }

    public CollectionUser(int collectionId, Guid userId)
    {
        CollectionId = collectionId;
        UserId = userId;
    }
}
