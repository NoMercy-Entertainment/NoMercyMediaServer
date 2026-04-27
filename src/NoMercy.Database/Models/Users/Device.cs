using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

[PrimaryKey(nameof(Id))]
[Index(nameof(DeviceId), IsUnique = true)]
public class Device : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("device_id")] public string DeviceId { get; set; } = string.Empty;
    [JsonProperty("browser")] public string Browser { get; set; } = string.Empty;
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;

    [Column("Device")]
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("type")] public string Type { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("custom_name")] public string? CustomName { get; set; }
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("ip")] public string Ip { get; set; } = string.Empty;
    [JsonProperty("activity_logs")] public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = [];

    [JsonProperty("is_active")] public bool IsActive { get; set; }
    [JsonProperty("volume_percent")] public int VolumePercent { get; set; }

    [JsonProperty("fingerprint")] public string? Fingerprint { get; set; }
    [JsonProperty("owner_user_id")] public Guid? OwnerUserId { get; set; }
    [JsonProperty("lan_ip")] public string? LanIp { get; set; }
    [JsonProperty("lan_port")] public int? LanPort { get; set; }
    [JsonProperty("ws_connected_at")] public DateTime? WsConnectedAt { get; set; }
    [JsonProperty("mdns_seen_at")] public DateTime? MdnsSeenAt { get; set; }

    public virtual User? OwnerUser { get; set; }
}