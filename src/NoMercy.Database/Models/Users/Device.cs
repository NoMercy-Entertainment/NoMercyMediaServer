// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(DeviceId), IsUnique = true)]
public class Device : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "browser")]
    public string Browser { get; set; } = string.Empty;

    [JsonProperty(propertyName: "os")]
    public string Os { get; set; } = string.Empty;

    [Column(name: "Device")]
    [JsonProperty(propertyName: "model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = null!;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "custom_name")]
    public string? CustomName { get; set; }

    [JsonProperty(propertyName: "version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty(propertyName: "ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonProperty(propertyName: "activity_logs")]
    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = [];

    [JsonProperty(propertyName: "is_active")]
    public bool IsActive { get; set; }

    public const int DefaultVolumePercent = 50;

    [JsonProperty(propertyName: "volume_percent")]
    public int? VolumePercent { get; set; }

    [JsonProperty(propertyName: "fingerprint")]
    public string? Fingerprint { get; set; }

    [JsonProperty(propertyName: "owner_user_id")]
    public Guid? OwnerUserId { get; set; }

    [JsonProperty(propertyName: "lan_ip")]
    public string? LanIp { get; set; }

    [JsonProperty(propertyName: "lan_port")]
    public int? LanPort { get; set; }

    [JsonProperty(propertyName: "ws_connected_at")]
    public DateTime? WsConnectedAt { get; set; }

    [JsonProperty(propertyName: "mdns_seen_at")]
    public DateTime? MdnsSeenAt { get; set; }

    [Column(TypeName = "TEXT")]
    [JsonProperty(propertyName: "capabilities_json")]
    public string? CapabilitiesJson { get; set; }

    public virtual User? OwnerUser { get; set; }
}
