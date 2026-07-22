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
/// Base implementation for container component props.
/// Container components can hold child components.
/// </summary>
public record ContainerProps : IContainerProps
{
    [JsonProperty(propertyName: "id")]
    public dynamic Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "next_id", NullValueHandling = NullValueHandling.Ignore)]
    public dynamic? NextId { get; set; }

    [JsonProperty(propertyName: "previous_id", NullValueHandling = NullValueHandling.Ignore)]
    public dynamic? PreviousId { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "more_link", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? MoreLink { get; set; }

    [JsonProperty(propertyName: "more_link_text", NullValueHandling = NullValueHandling.Ignore)]
    public string? MoreLinkText => MoreLink is not null ? "See all".Localize() : null;

    [JsonProperty(propertyName: "items")]
    public IEnumerable<ComponentEnvelope> Items { get; set; } = [];

    [JsonProperty(propertyName: "context_menu_items", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<ContextMenuItemDto>? ContextMenuItems { get; set; }

    [JsonProperty(propertyName: "url", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "properties", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, dynamic>? Properties { get; set; }
}

/// <summary>
/// Props for NMGrid component - displays items in a grid layout.
/// </summary>
public record GridProps : ContainerProps
{
    [JsonProperty(propertyName: "columns", NullValueHandling = NullValueHandling.Ignore)]
    public int? Columns { get; set; }

    [JsonProperty(propertyName: "gap", NullValueHandling = NullValueHandling.Ignore)]
    public int? Gap { get; set; }
}

/// <summary>
/// Props for NMList component - displays items in a vertical list.
/// </summary>
public record ListProps : ContainerProps
{
    [JsonProperty(propertyName: "orientation", NullValueHandling = NullValueHandling.Ignore)]
    public string? Orientation { get; set; }
}

/// <summary>
/// Props for NMCarousel component - displays items in a horizontal scrollable carousel.
/// </summary>
public record CarouselProps : ContainerProps
{
    [JsonProperty(propertyName: "auto_scroll", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AutoScroll { get; set; }

    [JsonProperty(propertyName: "scroll_interval", NullValueHandling = NullValueHandling.Ignore)]
    public int? ScrollInterval { get; set; }
}

/// <summary>
/// Props for NMContainer component - generic container for grouping components.
/// </summary>
public record NmContainerProps : ContainerProps
{
    [JsonProperty(propertyName: "layout", NullValueHandling = NullValueHandling.Ignore)]
    public string? Layout { get; set; }
}
