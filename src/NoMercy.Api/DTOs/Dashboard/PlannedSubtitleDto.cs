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
/// One subtitle output the profile intends to produce.
/// </summary>
public class PlannedSubtitleDto
{
    /// <summary><c>SubtitleCodecType</c> by name: WebVtt, Srt, Ass, Pgs, Copy.</summary>
    [JsonProperty("codec")]
    public string Codec { get; set; } = string.Empty;

    /// <summary><c>SubtitlePolicy</c> by name: Extract, BurnIn, Copy.</summary>
    [JsonProperty("policy")]
    public string Policy { get; set; } = string.Empty;

    /// <summary>
    /// ISO 639 codes this output accepts. Empty means every language in the
    /// source.
    /// </summary>
    [JsonProperty("languages")]
    public string[] Languages { get; set; } = [];
}
