
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class ActivityLog : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; private set; }

    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    public required DateTime Time { get; set; }

    [JsonProperty("device_id")] public Ulid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
 
    [JsonProperty("user_id")] public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
