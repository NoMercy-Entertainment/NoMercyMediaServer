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
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("url")]
    public string? Url { get; set; }

    [JsonProperty("isOfficial")]
    public bool IsOfficial { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty("score")]
    public int Score { get; set; }

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("imageIsFallback")]
    public bool ImageIsFallback { get; set; }

    [JsonProperty("remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty("tags")]
    public TvdbTagOption[]? Tags { get; set; }
}

public class TvdbListExtended : TvdbList
{
    [JsonProperty("entities")]
    public TvdbListEntity[]? Entities { get; set; }
}

public class TvdbListEntity
{
    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("seriesId")]
    public long? SeriesId { get; set; }

    [JsonProperty("movieId")]
    public long? MovieId { get; set; }
}
