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

namespace NoMercy.Cli.Models;

internal class PluginResponse
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty(propertyName: "version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty(propertyName: "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(propertyName: "author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty(propertyName: "project_url")]
    public string? ProjectUrl { get; set; }
}
