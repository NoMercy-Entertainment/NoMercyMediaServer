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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Wrapper for NMMusicCard component matching Android app expectations.
/// </summary>
public record NMMusicCardWrapper
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "next_id")]
    public string? NextId { get; set; }

    [JsonProperty(propertyName: "previous_id")]
    public string? PreviousId { get; set; }

    [JsonProperty(propertyName: "more_link")]
    public string? MoreLink { get; set; }

    [JsonProperty(propertyName: "more_link_text")]
    public string? MoreLinkText { get; set; }

    [JsonProperty(propertyName: "watch")]
    public bool Watch { get; set; }

    [JsonProperty(propertyName: "data")]
    public MusicCardData Data { get; set; } = null!;

    [JsonProperty(propertyName: "context_menu_items")]
    public IEnumerable<ContextMenuItem> ContextMenuItems { get; set; } = [];

    [JsonProperty(propertyName: "url")]
    public string? Url { get; set; }

    [JsonProperty(propertyName: "properties")]
    public Dictionary<string, string>? Properties { get; set; }

    public NMMusicCardWrapper() { }

    public NMMusicCardWrapper(MusicCardData musicCardData)
    {
        Id = musicCardData.Id.OrEmpty();
        Data = musicCardData;
    }
}
