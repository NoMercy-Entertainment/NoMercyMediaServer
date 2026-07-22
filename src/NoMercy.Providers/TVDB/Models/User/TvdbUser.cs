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
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "language")]
    public string? Language { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }
}

public class TvdbUserFavorites
{
    [JsonProperty(propertyName: "series")]
    public long[] Series { get; set; } = [];

    [JsonProperty(propertyName: "movies")]
    public long[] Movies { get; set; } = [];

    [JsonProperty(propertyName: "episodes")]
    public long[] Episodes { get; set; } = [];

    [JsonProperty(propertyName: "artwork")]
    public long[] Artwork { get; set; } = [];

    [JsonProperty(propertyName: "people")]
    public long[] People { get; set; } = [];

    [JsonProperty(propertyName: "lists")]
    public long[] Lists { get; set; } = [];
}
