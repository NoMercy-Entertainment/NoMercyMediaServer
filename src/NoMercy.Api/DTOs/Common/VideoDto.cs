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
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Api.DTOs.Common;

public record VideoDto
{
    [JsonProperty("src")]
    public string? Src { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("site")]
    public string? Site { get; set; }

    [JsonProperty("size")]
    public int Size { get; set; }

    public VideoDto(Database.Models.Media.Media media)
    {
        Src = media.Src;
        Type = media.Type;
        Name = media.Name;
        Site = media.Site;
        Size = media.Size;
    }

    public VideoDto(TmdbTvVideo media)
    {
        Src = media.Key;
        Type = "video";
        Name = media.Name;
        Site = media.Site;
        Size = media.Size;
    }

    public VideoDto(TmdbMovieVideo media)
    {
        Src = media.Key;
        Type = "video";
        Name = media.Name;
        Site = media.Site;
        Size = media.Size;
    }
}
