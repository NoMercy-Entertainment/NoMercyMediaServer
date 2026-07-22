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
/// Wrapper for NMCard and NMGenreCard components matching Android app expectations.
/// This is the props structure sent for both "NMCard" and "NMGenreCard" component types.
/// </summary>
public record NMCardWrapper
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "data")]
    public CardData? Data { get; set; }

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

    [JsonProperty(propertyName: "context_menu_items")]
    public IEnumerable<ContextMenuItem> ContextMenuItems { get; set; } = [];

    [JsonProperty(propertyName: "url")]
    public string? Url { get; set; }

    [JsonProperty(propertyName: "properties")]
    public Dictionary<string, string>? Properties { get; set; }

    public NMCardWrapper() { }

    public NMCardWrapper(CardData cardData)
    {
        Id = cardData.Id?.ToString() ?? string.Empty;
        Title = cardData.Title;
        Data = cardData;
    }
}
