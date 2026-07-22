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

using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public record ServerActivityRequest
{
    [JsonProperty(propertyName: "take")]
    public int? Take { get; set; } = 50;

    [JsonProperty(propertyName: "skip")]
    public int? Skip { get; set; } = 0;

    [JsonProperty(propertyName: "category")]
    public ActivityCategory? Category { get; set; }

    [JsonProperty(propertyName: "user_id")]
    public Guid? UserId { get; set; }

    [JsonProperty(propertyName: "device_id")]
    public Ulid? DeviceId { get; set; }

    [JsonProperty(propertyName: "media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty(propertyName: "from")]
    public DateTime? From { get; set; }

    [JsonProperty(propertyName: "to")]
    public DateTime? To { get; set; }

    [JsonProperty(propertyName: "success")]
    public bool? Success { get; set; }
}
