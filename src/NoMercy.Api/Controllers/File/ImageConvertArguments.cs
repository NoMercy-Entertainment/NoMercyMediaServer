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

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.File;

public class ImageConvertArguments
{
    [JsonProperty("width")]
    public int? Width { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("quality")]
    public int? Quality { get; set; }

    [FromQuery(Name = "aspect_ratio")]
    [JsonProperty("aspect_ratio")]
    public double? AspectRatio { get; set; }
}
