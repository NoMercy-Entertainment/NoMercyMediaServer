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

namespace NoMercy.Providers.TVDB.Models.Shared;

public class TvdbStatus
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "recordType")]
    public string? RecordType { get; set; }

    [JsonProperty(propertyName: "keepUpdated")]
    public bool KeepUpdated { get; set; }
}

public class TvdbRemoteId
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public int Type { get; set; }

    [JsonProperty(propertyName: "sourceName")]
    public string SourceName { get; set; } = string.Empty;
}

public class TvdbTagOption
{
    [JsonProperty(propertyName: "helpText")]
    public string? HelpText { get; set; }

    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "tag")]
    public int Tag { get; set; }

    [JsonProperty(propertyName: "tagName")]
    public string? TagName { get; set; }
}

public class TvdbTrailer
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty(propertyName: "runtime")]
    public int? Runtime { get; set; }
}

public class TvdbTranslationData
{
    [JsonProperty(propertyName: "aliases")]
    public string[]? Aliases { get; set; }

    [JsonProperty(propertyName: "isAlias")]
    public bool? IsAlias { get; set; }

    [JsonProperty(propertyName: "isPrimary")]
    public bool? IsPrimary { get; set; }

    [JsonProperty(propertyName: "language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "tagline")]
    public string? Tagline { get; set; }
}

public class TvdbTranslations
{
    [JsonProperty(propertyName: "nameTranslations")]
    public TvdbTranslationData[]? NameTranslations { get; set; }

    [JsonProperty(propertyName: "overviewTranslations")]
    public TvdbTranslationData[]? OverviewTranslations { get; set; }

    [JsonProperty(propertyName: "alias")]
    public string[]? Alias { get; set; }
}
