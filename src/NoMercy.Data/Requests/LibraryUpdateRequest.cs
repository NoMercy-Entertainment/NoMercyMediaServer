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
using NoMercy.Data.DTOs;

namespace NoMercy.Data.Requests;

public class LibraryUpdateRequest
{
    [JsonProperty(propertyName: "id")]
    public Ulid? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "image")]
    public string? Image { get; set; }

    [JsonProperty(propertyName: "perfectSubtitleMatch")]
    public bool? PerfectSubtitleMatch { get; set; }

    [JsonProperty(propertyName: "realtime")]
    public bool? Realtime { get; set; }

    [JsonProperty(propertyName: "autoEncodeOnScan")]
    public bool? AutoEncodeOnScan { get; set; }

    [JsonProperty(propertyName: "encodePresetId")]
    public Ulid? EncodePresetId { get; set; }

    [JsonProperty(propertyName: "specialSeasonName")]
    public string? SpecialSeasonName { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "folder_library")]
    public FolderLibraryDto[]? FolderLibrary { get; set; }

    [JsonProperty(propertyName: "subtitles")]
    public string[]? Subtitles { get; set; }
}
