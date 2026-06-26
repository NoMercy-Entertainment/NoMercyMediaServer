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

public class EncoderProfileDto
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("tags")]
    public string? Tags { get; set; }

    [JsonProperty("parent_preset_id")]
    public Ulid? ParentPresetId { get; set; }

    [JsonProperty("is_built_in")]
    public bool IsBuiltIn { get; set; }

    [JsonProperty("source")]
    public string Source { get; set; } = "db";

    [JsonProperty("profile_json")]
    public required string ProfileJson { get; set; }
}
