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
using NoMercy.NmSystem.Dto;

namespace NoMercy.Api.DTOs.Dashboard;

public record DirectoryTreeListing
{
    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = "ok";

    [JsonProperty(propertyName: "path", NullValueHandling = NullValueHandling.Ignore)]
    public string? Path { get; set; }

    [JsonProperty(propertyName: "parent")]
    public string? Parent { get; set; }

    [JsonProperty(propertyName: "data")]
    public List<DirectoryTree> Data { get; set; } = [];
}
