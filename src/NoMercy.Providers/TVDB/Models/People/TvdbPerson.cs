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
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("score")]
    public int Score { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }
}

public class TvdbPersonExtended : TvdbPerson
{
    [JsonProperty("birth")]
    public string? Birth { get; set; }

    [JsonProperty("birthPlace")]
    public string? BirthPlace { get; set; }

    [JsonProperty("death")]
    public string? Death { get; set; }

    [JsonProperty("gender")]
    public int Gender { get; set; }

    [JsonProperty("characters")]
    public TvdbCharacter[] Characters { get; set; } = [];

    [JsonProperty("races")]
    public string[] Races { get; set; } = [];

    [JsonProperty("remoteIds")]
    public TvdbRemoteId[] RemoteIds { get; set; } = [];

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("tagOptions")]
    public TvdbTagOption[] TagOptions { get; set; } = [];

    [JsonProperty("translations")]
    public TvdbTranslations? Translations { get; set; }

    [JsonProperty("awards")]
    public Awards.TvdbAward[] Awards { get; set; } = [];

    [JsonProperty("biographies")]
    public TvdbBiography[] Biographies { get; set; } = [];
}

public class TvdbBiography
{
    [JsonProperty("biography")]
    public string Biography { get; set; } = string.Empty;

    [JsonProperty("language")]
    public string Language { get; set; } = string.Empty;
}

public class TvdbPersonType
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
