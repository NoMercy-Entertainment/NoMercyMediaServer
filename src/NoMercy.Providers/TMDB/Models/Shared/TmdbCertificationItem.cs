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

public class TmdbCertificationItem
{
    [JsonProperty(propertyName: "certification")]
    public string Certification { get; set; } = string.Empty;

    [JsonProperty(propertyName: "meaning")]
    public string Meaning { get; set; } = string.Empty;

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }
    public string Iso31661 { get; set; } = string.Empty;
}
