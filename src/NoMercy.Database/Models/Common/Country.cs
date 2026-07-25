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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Common;

[Index(nameof(Iso31661), IsUnique = true)]
[Index(nameof(EnglishName))]
[Index(nameof(NativeName))]
[PrimaryKey(nameof(Id))]
public class Country
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [Key]
    [JsonProperty("iso_3166_1")]
    public string Iso31661 { get; set; } = string.Empty;

    [JsonProperty("english_name")]
    public string? EnglishName { get; set; }

    [JsonProperty("native_name")]
    public string? NativeName { get; set; }

    // public Country(Providers.TMDB.Models.Configuration.TmdbCountry tmdbCountry)
    // {
    //     Iso31661 = tmdbCountry.Iso31661;
    //     EnglishName = tmdbCountry.EnglishName;
    //     NativeName = tmdbCountry.NativeName;
    // }
}
