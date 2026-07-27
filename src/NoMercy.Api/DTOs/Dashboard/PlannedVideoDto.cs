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

/// <summary>
/// One video rendition the profile intends to produce. Codec names and heights
/// only — no sentence to translate, so every client writes its own label.
/// </summary>
public class PlannedVideoDto
{
    /// <summary><c>VideoCodecType</c> by name: H264, H265, Vp9, Av1, Copy.</summary>
    [JsonProperty("codec")]
    public string Codec { get; set; } = string.Empty;

    /// <summary>Rendition height in pixels. Null when it follows the source.</summary>
    [JsonProperty("height")]
    public int? Height { get; set; }
}
