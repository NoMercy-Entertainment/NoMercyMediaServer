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

namespace NoMercy.Database.Models.Movies;

[PrimaryKey(propertyName: nameof(CompanyId), additionalPropertyNames: nameof(MovieId))]
[Index(propertyName: nameof(CompanyId), additionalPropertyNames: nameof(MovieId), IsUnique = true)]
public class CompanyMovie : Timestamps
{
    [JsonProperty(propertyName: "company_id")]
    public int CompanyId { get; set; }

    [JsonProperty(propertyName: "company")]
    public Company Company { get; set; } = null!;

    [JsonProperty(propertyName: "movieid")]
    public int MovieId { get; set; }

    [JsonProperty(propertyName: "movie")]
    public Movie Movie { get; set; } = null!;
}
