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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Api.DTOs.Common;

public record AnimeDemographicDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("link")]
    public Uri Link { get; set; } = null!;

    public AnimeDemographicDto() { }

    public AnimeDemographicDto(AnimeDemographicMovie animeDemographicMovie)
    {
        Id = animeDemographicMovie.AnimeDemographicId;
        Name = animeDemographicMovie.AnimeDemographic.Name;
        Link = new($"/anime/demographics/{Id}", UriKind.Relative);
    }

    public AnimeDemographicDto(AnimeDemographicTv animeDemographicTv)
    {
        Id = animeDemographicTv.AnimeDemographicId;
        Name = animeDemographicTv.AnimeDemographic.Name;
        Link = new($"/anime/demographics/{Id}", UriKind.Relative);
    }
}
