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

namespace NoMercy.Database.Models.Security;

[PrimaryKey(nameof(Id))]
[Index(nameof(Address), IsUnique = true)]
[Index(nameof(ExpiresAt))]
public class IpBan : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonProperty("last_path")]
    public string? LastPath { get; set; }

    [JsonProperty("offence_count")]
    public int OffenceCount { get; set; }

    // Carried forward across bans of the same address so a repeat offender's
    // sentence keeps doubling after an earlier ban has already expired.
    [JsonProperty("ban_number")]
    public int BanNumber { get; set; } = 1;

    [JsonProperty("banned_at")]
    public DateTime BannedAt { get; set; }

    [JsonProperty("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [JsonProperty("manual")]
    public bool Manual { get; set; }
}
