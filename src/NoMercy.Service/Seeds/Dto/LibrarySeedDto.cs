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

namespace NoMercy.Service.Seeds.Dto;

public class LibrarySeedDto
{
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "image")]
    public string Image { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; } = 99;

    [JsonProperty(propertyName: "specialSeasonName")]
    public string SpecialSeasonName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "realtime")]
    public bool Realtime { get; set; }

    [JsonProperty(propertyName: "autoRefreshInterval")]
    public int AutoRefreshInterval { get; set; }

    [JsonProperty(propertyName: "chapterImages")]
    public bool ChapterImages { get; set; }

    [JsonProperty(propertyName: "extractChaptersDuring")]
    public bool ExtractChaptersDuring { get; set; }

    [JsonProperty(propertyName: "extractChapters")]
    public bool ExtractChapters { get; set; }

    [JsonProperty(propertyName: "perfectSubtitleMatch")]
    public bool PerfectSubtitleMatch { get; set; }

    [JsonProperty(propertyName: "folders")]
    public FolderSeedDto[] Folders { get; set; } = [];
}
