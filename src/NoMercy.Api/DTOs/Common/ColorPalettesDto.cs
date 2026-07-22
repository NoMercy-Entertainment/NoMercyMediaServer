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
using NoMercy.Database;

namespace NoMercy.Api.DTOs.Common;

public record ColorPalettesDto
{
    [JsonProperty(propertyName: "logo")]
    public ColorPalette Logo { get; set; } = new();

    [JsonProperty(propertyName: "poster")]
    public ColorPalette Poster { get; set; } = new();

    [JsonProperty(propertyName: "backdrop")]
    public ColorPalette Backdrop { get; set; } = new();
}
