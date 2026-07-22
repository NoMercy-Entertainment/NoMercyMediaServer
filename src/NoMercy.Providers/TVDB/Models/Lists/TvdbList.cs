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

namespace NoMercy.Providers.TVDB.Models.Lists;

public class TvdbListsResponse : TvdbResponse<TvdbList[]> { }

public class TvdbListResponse : TvdbResponse<TvdbList> { }

public class TvdbListExtendedResponse : TvdbResponse<TvdbListExtended> { }

public class TvdbListTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbList
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "url")]
    public string? Url { get; set; }

    [JsonProperty(propertyName: "isOfficial")]
    public bool IsOfficial { get; set; }

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "score")]
    public int Score { get; set; }

    [JsonProperty(propertyName: "image")]
    public Uri? Image { get; set; }

    [JsonProperty(propertyName: "imageIsFallback")]
    public bool ImageIsFallback { get; set; }

    [JsonProperty(propertyName: "remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty(propertyName: "tags")]
    public TvdbTagOption[]? Tags { get; set; }
}

public class TvdbListExtended : TvdbList
{
    [JsonProperty(propertyName: "entities")]
    public TvdbListEntity[]? Entities { get; set; }
}

public class TvdbListEntity
{
    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }

    [JsonProperty(propertyName: "seriesId")]
    public long? SeriesId { get; set; }

    [JsonProperty(propertyName: "movieId")]
    public long? MovieId { get; set; }
}
