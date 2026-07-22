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

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Context menu item for component actions.
/// </summary>
public record ContextMenuItem
{
    [JsonProperty(propertyName: "id")]
    public string? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "action")]
    public string? Action { get; set; }

    [JsonProperty(propertyName: "icon")]
    public string? Icon { get; set; }

    [JsonProperty(propertyName: "destructive")]
    public bool Destructive { get; set; }
}
