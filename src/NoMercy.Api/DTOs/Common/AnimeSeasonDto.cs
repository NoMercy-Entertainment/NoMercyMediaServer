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

public record AnimeSeasonDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("year")]
    public int Year { get; set; }

    [JsonProperty("quarter")]
    public string Quarter { get; set; } = string.Empty;

    [JsonProperty("link")]
    public Uri Link { get; set; } = null!;

    public AnimeSeasonDto() { }

    public AnimeSeasonDto(AnimeSeasonMovie animeSeasonMovie)
    {
        Id = animeSeasonMovie.AnimeSeasonId;
        Year = animeSeasonMovie.AnimeSeason.Year;
        Quarter = animeSeasonMovie.AnimeSeason.Quarter;
        Link = new($"/anime/seasons/{Id}", UriKind.Relative);
    }

    public AnimeSeasonDto(AnimeSeasonTv animeSeasonTv)
    {
        Id = animeSeasonTv.AnimeSeasonId;
        Year = animeSeasonTv.AnimeSeason.Year;
        Quarter = animeSeasonTv.AnimeSeason.Quarter;
        Link = new($"/anime/seasons/{Id}", UriKind.Relative);
    }
}
