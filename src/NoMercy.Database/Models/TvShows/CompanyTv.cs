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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(nameof(CompanyId), nameof(TvId))]
[Index(nameof(CompanyId), nameof(TvId), IsUnique = true)]
public class CompanyTv : Timestamps
{
    [JsonProperty("company_id")]
    public int CompanyId { get; set; }

    [JsonProperty("company")]
    public Company Company { get; set; } = null!;

    [JsonProperty("tvid")]
    public int TvId { get; set; }

    [JsonProperty("tv")]
    public Tv Tv { get; set; } = null!;
}
