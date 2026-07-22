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
/// Base implementation for leaf component props.
/// Leaf components hold data but cannot have children.
/// </summary>
/// <typeparam name="TData">The type of data this component displays.</typeparam>
public record LeafProps<TData> : ILeafProps<TData>
{
    [JsonProperty(propertyName: "id")]
    public dynamic Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "next_id", NullValueHandling = NullValueHandling.Ignore)]
    public dynamic? NextId { get; set; }

    [JsonProperty(propertyName: "previous_id", NullValueHandling = NullValueHandling.Ignore)]
    public dynamic? PreviousId { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "data")]
    public TData? Data { get; set; }

    [JsonProperty(propertyName: "watch")]
    public bool Watch { get; set; }

    [JsonProperty(propertyName: "context_menu_items", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<ContextMenuItemDto>? ContextMenuItems { get; set; }

    [JsonProperty(propertyName: "url", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "properties", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, dynamic>? Properties { get; set; }
}

/// <summary>
/// Props for NMCard component - standard media card.
/// </summary>
public record CardProps : LeafProps<CardData>;

/// <summary>
/// Props for NMHomeCard component - featured home page card with video support.
/// </summary>
public record HomeCardProps : LeafProps<HomeCardData>;

/// <summary>
/// Props for NMGenreCard component - genre category card.
/// </summary>
public record GenreCardProps : LeafProps<GenreCardData>;

/// <summary>
/// Props for NMMusicCard component - music album/artist card.
/// </summary>
public record MusicCardProps : LeafProps<MusicCardData>;

/// <summary>
/// Props for NMMusicHomeCard component - music home featured card.
/// </summary>
public record MusicHomeCardProps : LeafProps<MusicHomeCardData>;

/// <summary>
/// Props for NMTrackRow component - single track in a list.
/// </summary>
public record TrackRowProps : LeafProps<TrackRowData>
{
    [JsonProperty(propertyName: "displayList", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<TrackRowData>? DisplayList { get; set; }
}

/// <summary>
/// Props for NMTopResultCard component - search top result.
/// </summary>
public record TopResultCardProps : LeafProps<TopResultCardData>;

/// <summary>
/// Props for NMSeasonCard component - episode in a season.
/// </summary>
public record SeasonCardProps : LeafProps<SeasonCardData>;

/// <summary>
/// Props for NMSeasonTitle component - season header.
/// </summary>
public record SeasonTitleProps : LeafProps<SeasonTitleData>;

/// <summary>
/// Props for NMEmptyState component - shown when there is no content to display.
/// </summary>
public record EmptyStateProps : LeafProps<EmptyStateData>;
