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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.User;

public class TvdbUserResponse : TvdbResponse<TvdbUser> { }

public class TvdbUserFavoritesResponse : TvdbResponse<TvdbUserFavorites> { }

public class TvdbUser
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("language")]
    public string? Language { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }
}

public class TvdbUserFavorites
{
    [JsonProperty("series")]
    public long[] Series { get; set; } = [];

    [JsonProperty("movies")]
    public long[] Movies { get; set; } = [];

    [JsonProperty("episodes")]
    public long[] Episodes { get; set; } = [];

    [JsonProperty("artwork")]
    public long[] Artwork { get; set; } = [];

    [JsonProperty("people")]
    public long[] People { get; set; } = [];

    [JsonProperty("lists")]
    public long[] Lists { get; set; } = [];
}
