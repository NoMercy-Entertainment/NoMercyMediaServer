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

namespace NoMercy.Database.Models.Common;

[PrimaryKey(nameof(Id))]
[Index(nameof(Iso31661), nameof(Rating), IsUnique = true)]
[Index(nameof(Rating))]
[Index(nameof(Order))]
public class Certification
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("iso_3166_1")]
    public string? Iso31661 { get; set; } = string.Empty;

    [JsonProperty("rating")]
    public string? Rating { get; set; } = string.Empty;

    [JsonProperty("meaning")]
    public string Meaning { get; set; } = string.Empty;

    [JsonProperty("order")]
    public int Order { get; set; }

    // public Certification(string? country, TmdbTvShowCertification certification)
    // {
    //     Iso31661 = country;
    //     Rating = certification.Rating;
    //     Meaning = certification.Meaning;
    //     Order = certification.Order;
    // }
    //
    // public Certification(string? country, TmdbMovieCertification certification)
    // {
    //     Iso31661 = country;
    //     Rating = certification.Rating;
    //     Meaning = certification.Meaning;
    //     Order = certification.Order;
    // }
}
