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
using NoMercy.Providers.TVDB.Models.Movies;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Awards;

public class TvdbAwardsResponse : TvdbResponse<TvdbAward[]> { }

public class TvdbAwardResponse : TvdbResponse<TvdbAward> { }

public class TvdbAwardExtendedResponse : TvdbResponse<TvdbAwardExtended> { }

public class TvdbAwardCategoryResponse : TvdbResponse<TvdbAwardCategory> { }

public class TvdbAwardCategoryExtendedResponse : TvdbResponse<TvdbAwardCategoryExtended> { }

public class TvdbAward
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;
}

public class TvdbAwardExtended
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "score")]
    public int Score { get; set; }

    [JsonProperty(propertyName: "categories")]
    public List<TvdbAwardCategory> Categories { get; set; } = [];
}

public class TvdbAwardCategory
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "allowCoNominees")]
    public bool AllowCoNominees { get; set; }

    [JsonProperty(propertyName: "award")]
    public TvdbAward Award { get; set; } = new();

    [JsonProperty(propertyName: "forMovies")]
    public bool ForMovies { get; set; }

    [JsonProperty(propertyName: "forSeries")]
    public bool ForSeries { get; set; }
}

public class TvdbAwardCategoryExtended : TvdbAwardCategory
{
    [JsonProperty(propertyName: "nominees")]
    public List<TvdbNominee> Nominees { get; set; } = [];
}

public class TvdbNominee
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "year")]
    public int Year { get; set; }

    [JsonProperty(propertyName: "details")]
    public string? Details { get; set; }

    [JsonProperty(propertyName: "isWinner")]
    public bool IsWinner { get; set; }

    [JsonProperty(propertyName: "category")]
    public string? Category { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "series")]
    public string? Series { get; set; }

    [JsonProperty(propertyName: "movie")]
    public TvdbMovie? Movie { get; set; }
}
