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

[PrimaryKey(propertyName: nameof(CompanyId), additionalPropertyNames: nameof(TvId))]
[Index(propertyName: nameof(CompanyId), additionalPropertyNames: nameof(TvId), IsUnique = true)]
public class CompanyTv : Timestamps
{
    [JsonProperty(propertyName: "company_id")]
    public int CompanyId { get; set; }

    [JsonProperty(propertyName: "company")]
    public Company Company { get; set; } = null!;

    [JsonProperty(propertyName: "tvid")]
    public int TvId { get; set; }

    [JsonProperty(propertyName: "tv")]
    public Tv Tv { get; set; } = null!;
}
