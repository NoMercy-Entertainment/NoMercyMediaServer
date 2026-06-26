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
using NoMercy.Database;
using NoMercy.Database.Models.People;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.DTOs.Media;

public record DirectorDto
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("color_palette")]
    public IColorPalettes? ColorPalette { get; set; }

    public DirectorDto(Crew crew)
    {
        Id = crew.Person.Id;
        Name = crew.Person.Name;
        ColorPalette = crew.Person.ColorPalette;
    }

    public DirectorDto(TmdbCrew tmdbCrew)
    {
        Id = tmdbCrew.Id;
        Name = tmdbCrew.Name;
        ColorPalette = new();
    }
}
