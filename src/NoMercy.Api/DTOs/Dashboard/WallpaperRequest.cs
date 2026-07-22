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
using NoMercy.NmSystem.Wallpaper;

namespace NoMercy.Api.DTOs.Dashboard;

public record WallpaperRequest
{
    [JsonProperty(propertyName: "path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty(propertyName: "color")]
    public string? Color { get; set; } = string.Empty;

    [JsonProperty(propertyName: "style")]
    public WallpaperStyle Style { get; set; } = WallpaperStyle.Fill;
}
