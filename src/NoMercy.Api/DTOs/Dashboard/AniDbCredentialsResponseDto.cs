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

namespace NoMercy.Api.DTOs.Dashboard;

public record AniDbCredentialsResponseDto
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("api_key")]
    public string? ApiKey { get; set; }
}
