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

namespace NoMercy.Service.Seeds.Dto;

public class ServerUserDto
{
    [JsonProperty(propertyName: "data")]
    public ServerUserDtoData[] Data { get; set; } = [];
}

public class ServerUserDtoData
{
    [JsonProperty(propertyName: "user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty(propertyName: "enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty(propertyName: "avatar")]
    public Uri? Avatar { get; set; }

    [JsonProperty(propertyName: "is_owner")]
    public bool IsOwner { get; set; }
}
