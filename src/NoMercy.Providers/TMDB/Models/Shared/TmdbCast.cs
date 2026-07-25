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

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCast
{
    [JsonProperty("adult")]
    public bool Adult { get; set; }

    [JsonProperty("gender")]
    public int Gender { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("known_for_department")]
    public string? KnownForDepartment { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("original_name")]
    public string? OriginalName { get; set; }

    [JsonProperty("popularity")]
    public float Popularity { get; set; }

    [JsonProperty("profile_path")]
    public string? ProfilePath { get; set; }

    [JsonProperty("character")]
    public string? Character { get; set; }

    [JsonProperty("credit_id")]
    public string? CreditId { get; set; }

    [JsonProperty("order")]
    public int? Order { get; set; }
}
