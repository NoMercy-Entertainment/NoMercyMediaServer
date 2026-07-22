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

[PrimaryKey(propertyName: nameof(CertificationId), additionalPropertyNames: nameof(MovieId))]
[Index(propertyName: nameof(CertificationId))]
[Index(propertyName: nameof(MovieId))]
public class CertificationMovie
{
    [JsonProperty(propertyName: "certification_id")]
    public int CertificationId { get; set; }
    public Certification Certification { get; set; } = null!;

    [JsonProperty(propertyName: "movie_id")]
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
}
