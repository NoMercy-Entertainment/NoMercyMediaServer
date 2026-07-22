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
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "personName")]
    public string PersonName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "aliases")]
    public List<TvdbAlias> Aliases { get; set; } = [];

    [JsonProperty(propertyName: "episodeId")]
    public long? EpisodeId { get; set; }

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "isFeatured")]
    public bool IsFeatured { get; set; }

    [JsonProperty(propertyName: "movieId")]
    public long? MovieId { get; set; }

    [JsonProperty(propertyName: "movie")]
    public TvdbInfo? Movie { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "peopleId")]
    public long PeopleId { get; set; }

    [JsonProperty(propertyName: "personImgURL")]
    public Uri? PersonImgUrl { get; set; }

    [JsonProperty(propertyName: "peopleType")]
    public string PeopleType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "seriesId")]
    public long? SeriesId { get; set; }

    [JsonProperty(propertyName: "series")]
    public TvdbInfo? Series { get; set; }

    [JsonProperty(propertyName: "sort")]
    public int Sort { get; set; }

    [JsonProperty(propertyName: "tagOptions")]
    public List<TvdbTagOption> TagOptions { get; set; } = [];

    [JsonProperty(propertyName: "type")]
    public int Type { get; set; }

    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }
}
