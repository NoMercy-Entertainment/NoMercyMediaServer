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
using NoMercy.Providers.TVDB.Models.Characters;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.People;

public class TvdbPersonResponse : TvdbResponse<TvdbPerson> { }

public class TvdbPersonExtendedResponse : TvdbResponse<TvdbPersonExtended> { }

public class TvdbPersonTypesResponse : TvdbResponse<TvdbPersonType[]> { }

public class TvdbPersonTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbPerson
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "score")]
    public int Score { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }
}

public class TvdbPersonExtended : TvdbPerson
{
    [JsonProperty(propertyName: "birth")]
    public string? Birth { get; set; }

    [JsonProperty(propertyName: "birthPlace")]
    public string? BirthPlace { get; set; }

    [JsonProperty(propertyName: "death")]
    public string? Death { get; set; }

    [JsonProperty(propertyName: "gender")]
    public int Gender { get; set; }

    [JsonProperty(propertyName: "characters")]
    public TvdbCharacter[] Characters { get; set; } = [];

    [JsonProperty(propertyName: "races")]
    public string[] Races { get; set; } = [];

    [JsonProperty(propertyName: "remoteIds")]
    public TvdbRemoteId[] RemoteIds { get; set; } = [];

    [JsonProperty(propertyName: "slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tagOptions")]
    public TvdbTagOption[] TagOptions { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public TvdbTranslations? Translations { get; set; }

    [JsonProperty(propertyName: "awards")]
    public Awards.TvdbAward[] Awards { get; set; } = [];

    [JsonProperty(propertyName: "biographies")]
    public TvdbBiography[] Biographies { get; set; } = [];
}

public class TvdbBiography
{
    [JsonProperty(propertyName: "biography")]
    public string Biography { get; set; } = string.Empty;

    [JsonProperty(propertyName: "language")]
    public string Language { get; set; } = string.Empty;
}

public class TvdbPersonType
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;
}
