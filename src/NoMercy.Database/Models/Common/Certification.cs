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

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Iso31661), additionalPropertyNames: nameof(Rating), IsUnique = true)]
[Index(propertyName: nameof(Rating))]
[Index(propertyName: nameof(Order))]
public class Certification
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "iso_3166_1")]
    public string? Iso31661 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "rating")]
    public string? Rating { get; set; } = string.Empty;

    [JsonProperty(propertyName: "meaning")]
    public string Meaning { get; set; } = string.Empty;

    [JsonProperty(propertyName: "order")]
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
