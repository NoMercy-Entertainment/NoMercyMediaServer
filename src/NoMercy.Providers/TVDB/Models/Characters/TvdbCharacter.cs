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

namespace NoMercy.Providers.TVDB.Models.Characters;

public class TvdbCharacterResponse : TvdbResponse<TvdbCharacter> { }

public class TvdbCharacter
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("personName")]
    public string PersonName { get; set; } = string.Empty;

    [JsonProperty("aliases")]
    public List<TvdbAlias> Aliases { get; set; } = [];

    [JsonProperty("episodeId")]
    public long? EpisodeId { get; set; }

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("isFeatured")]
    public bool IsFeatured { get; set; }

    [JsonProperty("movieId")]
    public long? MovieId { get; set; }

    [JsonProperty("movie")]
    public TvdbInfo? Movie { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("peopleId")]
    public long PeopleId { get; set; }

    [JsonProperty("personImgURL")]
    public Uri? PersonImgUrl { get; set; }

    [JsonProperty("peopleType")]
    public string PeopleType { get; set; } = string.Empty;

    [JsonProperty("seriesId")]
    public long? SeriesId { get; set; }

    [JsonProperty("series")]
    public TvdbInfo? Series { get; set; }

    [JsonProperty("sort")]
    public int Sort { get; set; }

    [JsonProperty("tagOptions")]
    public List<TvdbTagOption> TagOptions { get; set; } = [];

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("url")]
    public Uri? Url { get; set; }
}
