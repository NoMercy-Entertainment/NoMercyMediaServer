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
    [JsonProperty("id")]
    public Ulid? Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("image")]
    public string? Image { get; set; }

    [JsonProperty("perfectSubtitleMatch")]
    public bool? PerfectSubtitleMatch { get; set; }

    [JsonProperty("realtime")]
    public bool? Realtime { get; set; }

    [JsonProperty("specialSeasonName")]
    public string? SpecialSeasonName { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("folder_library")]
    public FolderLibraryDto[]? FolderLibrary { get; set; }

    [JsonProperty("subtitles")]
    public string[]? Subtitles { get; set; }
}
