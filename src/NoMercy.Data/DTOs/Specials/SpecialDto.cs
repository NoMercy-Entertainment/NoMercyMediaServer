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

namespace NoMercy.Data.DTOs.Specials;

public class SpecialDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "backdrop")]
    public string Backdrop { get; set; } = string.Empty;

    [JsonProperty(propertyName: "poster")]
    public string Poster { get; set; } = string.Empty;

    [JsonProperty(propertyName: "logo")]
    public string Logo { get; set; } = string.Empty;

    [JsonProperty(propertyName: "description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty(propertyName: "Item")]
    public SpecialItemDto[] Item { get; set; } = [];

    [JsonProperty(propertyName: "creator")]
    public string Creator { get; set; } = string.Empty;
}
