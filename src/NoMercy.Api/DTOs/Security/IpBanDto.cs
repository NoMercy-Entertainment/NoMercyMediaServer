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
using NoMercy.Database.Models.Security;

namespace NoMercy.Api.DTOs.Security;

public class IpBanDto
{
    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonProperty("last_path")]
    public string? LastPath { get; set; }

    [JsonProperty("offence_count")]
    public int OffenceCount { get; set; }

    [JsonProperty("ban_number")]
    public int BanNumber { get; set; }

    [JsonProperty("manual")]
    public bool Manual { get; set; }

    [JsonProperty("banned_at")]
    public DateTime BannedAt { get; set; }

    [JsonProperty("expires_at")]
    public DateTime ExpiresAt { get; set; }

    public static IpBanDto From(IpBan ban) =>
        new()
        {
            Address = ban.Address,
            Reason = ban.Reason,
            LastPath = ban.LastPath,
            OffenceCount = ban.OffenceCount,
            BanNumber = ban.BanNumber,
            Manual = ban.Manual,
            BannedAt = ban.BannedAt,
            ExpiresAt = ban.ExpiresAt,
        };
}
