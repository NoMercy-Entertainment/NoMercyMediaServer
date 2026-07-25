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

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchRankColors
{
    [JsonProperty("rank_color_10")]
    public string RankColor10 { get; set; } = string.Empty;

    [JsonProperty("rank_color_50")]
    public string RankColor50 { get; set; } = string.Empty;

    [JsonProperty("rank_color_100")]
    public string RankColor100 { get; set; } = string.Empty;

    [JsonProperty("rank_color_200")]
    public string RankColor200 { get; set; } = string.Empty;
}
