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
    [JsonProperty("take")]
    public int? Take { get; set; } = 50;

    [JsonProperty("skip")]
    public int? Skip { get; set; } = 0;

    [JsonProperty("category")]
    public ActivityCategory? Category { get; set; }

    [JsonProperty("user_id")]
    public Guid? UserId { get; set; }

    [JsonProperty("device_id")]
    public Ulid? DeviceId { get; set; }

    [JsonProperty("media_id")]
    public Ulid? MediaId { get; set; }

    [JsonProperty("from")]
    public DateTime? From { get; set; }

    [JsonProperty("to")]
    public DateTime? To { get; set; }

    [JsonProperty("success")]
    public bool? Success { get; set; }
}
