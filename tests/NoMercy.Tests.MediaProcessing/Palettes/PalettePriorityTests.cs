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

using NoMercy.MediaProcessing.Images.Palettes;

namespace NoMercy.Tests.MediaProcessing.Palettes;

/// <summary>
/// The queue reserves work by OrderByDescending(Priority), so a higher number
/// must run first. These pin that every palette tier is numbered in the order it
/// should drain — the values were once inverted, so a live import painted its
/// images before the show/movie/episode they belong to, and the backfill
/// coordinator outranked the live imports it is meant to yield to.
/// </summary>
public class PalettePriorityTests
{
    [Fact]
    public void Import_paints_the_main_entity_before_its_images()
    {
        // The reported bug: an image (entityId 29076) reserved ahead of the
        // episode (6111114) it belongs to.
        PalettePriority
            .ForImport("episode")
            .Should()
            .BeGreaterThan(
                PalettePriority.ForImport("image"),
                "a live import must paint the episode before its images"
            );

        foreach (string main in new[] { "movie", "tv", "season", "episode", "artist", "album" })
            PalettePriority
                .ForImport(main)
                .Should()
                .BeGreaterThan(PalettePriority.ForImport("image"), $"{main} outranks its images");
    }

    [Fact]
    public void Tiers_drain_in_order_on_demand_import_backfill_coordinator()
    {
        PalettePriority.OnDemand.Should().BeGreaterThan(PalettePriority.ForImport("episode"));
        PalettePriority
            .ForImport("episode")
            .Should()
            .BeGreaterThan(PalettePriority.ForImport("image"));
        PalettePriority
            .ForImport("image")
            .Should()
            .BeGreaterThan(
                PalettePriority.ForBackfill("episode"),
                "a live import outranks any backfill"
            );
        PalettePriority
            .ForBackfill("episode")
            .Should()
            .BeGreaterThan(PalettePriority.ForBackfill("image"));
        PalettePriority
            .ForBackfill("image")
            .Should()
            .BeGreaterThan(
                PalettePriority.BackfillCoordinator,
                "the coordinator yields to the jobs it dispatches"
            );
    }

    [Fact]
    public void Image_is_not_treated_as_a_main_entity()
    {
        PalettePriority.IsMain("image").Should().BeFalse();
        PalettePriority.IsMain("episode").Should().BeTrue();
    }
}
