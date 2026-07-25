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

namespace NoMercy.Database.Models.Libraries;

[Index(nameof(Iso6391), IsUnique = true)]
[Index(nameof(EnglishName))]
[Index(nameof(Name))]
[PrimaryKey(nameof(Id))]
public class Language
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("iso_639_1")]
    public string Iso6391 { get; set; } = string.Empty;

    [JsonProperty("english_name")]
    public string EnglishName { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("language_library")]
    public ICollection<LanguageLibrary> LanguageLibrary { get; set; } = [];

    // public Language(Providers.TMDB.Models.Configuration.TmdbLanguage tmdbLanguage)
    // {
    //     Iso6391 = tmdbLanguage.Iso6391;
    //     EnglishName = tmdbLanguage.EnglishName;
    //     Name = tmdbLanguage.Name;
    // }
}
