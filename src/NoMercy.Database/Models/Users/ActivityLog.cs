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

[PrimaryKey(nameof(Id))]
[Index(nameof(UserId))]
[Index(nameof(DeviceId))]
[Index(nameof(Category))]
[Index(nameof(MediaId))]
public class ActivityLog : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; private set; }

    [JsonProperty("category")]
    public required ActivityCategory Category { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("time")]
    public required DateTime Time { get; set; }

    [JsonProperty("media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; } = true;

    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }

    [JsonProperty("metadata")]
    public string? Metadata { get; set; }

    [JsonProperty("device_id")]
    public Ulid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
