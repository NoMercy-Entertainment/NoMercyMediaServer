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
/// One audio track the profile intends to produce, per source language it
/// accepts.
/// </summary>
public class PlannedAudioDto
{
    /// <summary><c>AudioCodecType</c> by name: Aac, Eac3, Opus, Copy, …</summary>
    [JsonProperty("codec")]
    public string Codec { get; set; } = string.Empty;

    [JsonProperty("channels")]
    public int Channels { get; set; }

    /// <summary>Zero on a copied track, which keeps the source bitrate.</summary>
    [JsonProperty("bitrate_kbps")]
    public int BitrateKbps { get; set; }

    /// <summary>
    /// ISO 639 codes the track accepts. Empty means every language in the
    /// source, which is the profile saying it does not filter.
    /// </summary>
    [JsonProperty("languages")]
    public string[] Languages { get; set; } = [];
}
