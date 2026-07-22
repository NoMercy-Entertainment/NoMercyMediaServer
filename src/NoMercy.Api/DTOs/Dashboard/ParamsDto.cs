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

namespace NoMercy.Api.DTOs.Dashboard;

public record ParamsDto
{
    [JsonProperty(propertyName: "video")]
    public int Width { get; set; }

    [JsonProperty(propertyName: "crf")]
    public int Crf { get; set; }

    [JsonProperty(propertyName: "preset")]
    public string Preset { get; set; } = string.Empty;

    [JsonProperty(propertyName: "profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonProperty(propertyName: "codec")]
    public string Codec { get; set; } = string.Empty;

    [JsonProperty(propertyName: "audio")]
    public string Audio { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tune")]
    public string Tune { get; set; } = string.Empty;

    [JsonProperty(propertyName: "level")]
    public string Level { get; set; } = string.Empty;
}
