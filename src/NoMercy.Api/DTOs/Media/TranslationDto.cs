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
using NoMercy.Database.Models.Media;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Models.Combined;

namespace NoMercy.Api.DTOs.Media;

public record TranslationDto
{
    public TranslationDto(Translation translation)
    {
        Iso31661 = translation.Iso31661.OrEmpty();
        Iso6391 = translation.Iso6391.OrEmpty();
        EnglishName = translation.EnglishName.OrEmpty();
        Name = translation.Name.OrEmpty();
        Biography = translation.Biography.OrEmpty();
    }

    public TranslationDto(TmdbCombinedTranslation translation)
    {
        Iso31661 = translation.Iso31661;
        Iso6391 = translation.Iso6391;
        EnglishName = translation.EnglishName;
        Name = translation.Data.Name.OrEmpty();
        Biography = translation.Data.Biography.OrEmpty();
    }

    [JsonProperty(propertyName: "iso_3166_1")]
    public string Iso31661 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "iso_639_1")]
    public string Iso6391 { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "english_name")]
    public string EnglishName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "biography")]
    public string Biography { get; set; } = string.Empty;
}
