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

namespace NoMercy.Api.DTOs.Music;

public class ImageUploadResponseDto
{
    [JsonProperty("url")]
    public Uri Url { get; set; } = null!;

    [JsonProperty("color_palette")]
    public ColorPalette? ColorPalette { get; set; }
}
