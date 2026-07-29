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
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Api.DTOs.Dashboard;

/// <summary>What the owner is consenting to, in one call.</summary>
public class PluginConsentRequestDto
{
    [JsonProperty("grants")]
    public List<PluginGrantDto> Grants { get; set; } = [];
}

public class PluginGrantDto
{
    [JsonProperty("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;
}

public class PluginGrantDecisionDto : PluginGrantDto
{
    [JsonProperty("granted")]
    public bool Granted { get; set; }
}

/// <summary>A plugin's outstanding request, as the dashboard shows it.</summary>
public class PluginGrantRequestDto(PluginGrantRequest request)
{
    [JsonProperty("plugin_id")]
    public Guid PluginId { get; } = request.PluginId;

    [JsonProperty("kind")]
    public string Kind { get; } = request.Kind;

    [JsonProperty("value")]
    public string Value { get; } = request.Value;

    /// <summary>The plugin's own words. Untrusted — escape it where it renders.</summary>
    [JsonProperty("reason")]
    public string Reason { get; } = request.Reason;

    [JsonProperty("requested_at")]
    public DateTime RequestedAt { get; } = request.RequestedAt;
}
