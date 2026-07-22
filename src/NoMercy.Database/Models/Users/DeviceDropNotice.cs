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

namespace NoMercy.Database.Models.Users;

public class DeviceDropNotice : Timestamps
{
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }

    [JsonProperty(propertyName: "device_name")]
    public string DeviceName { get; set; } = "";

    [JsonProperty(propertyName: "reason")]
    public string Reason { get; set; } = ""; // "ttl" | "efuse" | "manual"

    [JsonProperty(propertyName: "acknowledged")]
    public bool Acknowledged { get; set; }
}
