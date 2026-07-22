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

namespace NoMercy.Database.Models.Libraries;

public sealed class CandidateMatch
{
    [JsonProperty(propertyName: "provider")]
    public required string Provider { get; set; }

    [JsonProperty(propertyName: "external_id")]
    public required string ExternalId { get; set; }

    [JsonProperty(propertyName: "title")]
    public required string Title { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "poster_path")]
    public string? PosterPath { get; set; }

    [JsonProperty(propertyName: "score")]
    public double Score { get; set; }
}
