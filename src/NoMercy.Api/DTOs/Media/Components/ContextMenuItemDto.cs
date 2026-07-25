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

public record ContextMenuItemDto
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("action")]
    public string? Action { get; set; }

    [JsonProperty("icon")]
    public string? Icon { get; set; }

    [JsonProperty("destructive")]
    public bool Destructive { get; set; }

    [JsonProperty("method")]
    public string? Method { get; set; }

    [JsonProperty("confirm")]
    public string? Confirm { get; set; }

    [JsonProperty("args")]
    public Dictionary<string, object>? Args { get; set; }
}
