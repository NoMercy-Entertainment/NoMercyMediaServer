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
[Index(propertyName: nameof(UserId))]
[Index(propertyName: nameof(DeviceId))]
[Index(propertyName: nameof(Category))]
[Index(propertyName: nameof(MediaId))]
public class ActivityLog : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; private set; }

    [JsonProperty(propertyName: "category")]
    public required ActivityCategory Category { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "time")]
    public required DateTime Time { get; set; }

    [JsonProperty(propertyName: "media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty(propertyName: "success")]
    public bool Success { get; set; } = true;

    [JsonProperty(propertyName: "error_code")]
    public string? ErrorCode { get; set; }

    [JsonProperty(propertyName: "metadata")]
    public string? Metadata { get; set; }

    [JsonProperty(propertyName: "device_id")]
    public Ulid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
