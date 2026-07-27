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

namespace NoMercy.Api.DTOs.Security;

public class AbuseGuardSettingsDto
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("max_score")]
    public int MaxScore { get; set; }

    [JsonProperty("window_minutes")]
    public int WindowMinutes { get; set; }

    [JsonProperty("ban_minutes")]
    public int BanMinutes { get; set; }

    [JsonProperty("max_ban_minutes")]
    public int MaxBanMinutes { get; set; }

    [JsonProperty("allowlist")]
    public string Allowlist { get; set; } = string.Empty;
}
