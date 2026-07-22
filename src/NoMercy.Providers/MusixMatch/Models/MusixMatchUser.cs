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

public class MusixMatchUser
{
    [JsonProperty(propertyName: "uaid")]
    public string Uaid { get; set; } = string.Empty;

    [JsonProperty(propertyName: "is_mine")]
    public int IsMine { get; set; }

    [JsonProperty(propertyName: "user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "user_profile_photo")]
    public string UserProfilePhoto { get; set; } = string.Empty;

    [JsonProperty(propertyName: "has_private_profile")]
    public int HasPrivateProfile { get; set; }

    [JsonProperty(propertyName: "score")]
    public int Score { get; set; }

    [JsonProperty(propertyName: "position")]
    public int Position { get; set; }

    [JsonProperty(propertyName: "weekly_score")]
    public int WeeklyScore { get; set; }

    [JsonProperty(propertyName: "level")]
    public string Level { get; set; } = string.Empty;

    [JsonProperty(propertyName: "key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty(propertyName: "rank_level")]
    public int RankLevel { get; set; }

    [JsonProperty(propertyName: "points_to_next_level")]
    public int PointsToNextLevel { get; set; }

    [JsonProperty(propertyName: "ratio_to_next_level")]
    public double RatioToNextLevel { get; set; }

    [JsonProperty(propertyName: "rank_name")]
    public string RankName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "next_rank_name")]
    public string NextRankName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "ratio_to_next_rank")]
    public double RatioToNextRank { get; set; }

    [JsonProperty(propertyName: "rank_color")]
    public string RankColor { get; set; } = string.Empty;

    [JsonProperty(propertyName: "rank_colors")]
    public MusixMatchRankColors MusixMatchRankColors { get; set; } = new();

    [JsonProperty(propertyName: "rank_image_url")]
    public string RankImageUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "next_rank_color")]
    public string NextRankColor { get; set; } = string.Empty;

    [JsonProperty(propertyName: "next_rank_colors")]
    public MusixMatchRankColors NextMusixMatchRankColors { get; set; } = new();

    [JsonProperty(propertyName: "next_rank_image_url")]
    public string NextRankImageUrl { get; set; } = string.Empty;

    [JsonProperty(propertyName: "counters")]
    public MusixMatchCounters MusixMatchCounters { get; set; } = new();

    [JsonProperty(propertyName: "academy_completed")]
    public bool AcademyCompleted { get; set; }

    [JsonProperty(propertyName: "moderator")]
    public bool Moderator { get; set; }
}
