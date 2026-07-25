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

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Defines all available component types in the system.
/// Container types can hold child components, leaf types cannot.
/// </summary>
public static class ComponentTypes
{
    // Container components - can hold child components
    public const string Grid = "NMGrid";
    public const string List = "NMList";
    public const string Carousel = "NMCarousel";
    public const string Container = "NMContainer";

    // Leaf components - cannot hold children
    public const string Card = "NMCard";
    public const string HomeCard = "NMHomeCard";
    public const string GenreCard = "NMGenreCard";
    public const string MusicCard = "NMMusicCard";
    public const string MusicHomeCard = "NMMusicHomeCard";
    public const string TrackRow = "NMTrackRow";
    public const string TopResultCard = "NMTopResultCard";
    public const string SeasonCard = "NMSeasonCard";
    public const string SeasonTitle = "NMSeasonTitle";
    public const string EmptyState = "NMEmptyState";

    public static bool IsContainer(string componentType) =>
        componentType is Grid or List or Carousel or Container;

    public static bool IsLeaf(string componentType) => !IsContainer(componentType);
}
