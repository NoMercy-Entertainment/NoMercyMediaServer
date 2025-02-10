
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(NotificationId), nameof(UserId))]
[Index(nameof(NotificationId))]
[Index(nameof(UserId))]
public class NotificationUser
{
    [JsonProperty("notification_id")] public Ulid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationUser()
    {
        //
    }

    public NotificationUser(Ulid notificationId, Guid userId)
    {
        NotificationId = notificationId;
        UserId = userId;
    }
}
