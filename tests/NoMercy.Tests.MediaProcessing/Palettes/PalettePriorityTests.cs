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
/// must run first. Values are small (0-10) and UI-first: whatever a viewer sees
/// first — movie/tv/artist/album, then episode/track, then season, then
/// supporting art like person/collection/image — paints first, in every band.
/// These pin the band ordering (on-demand > import > backfill > coordinator)
/// and the entity-rank ordering within each band.
/// </summary>
public class PalettePriorityTests
{
    private static readonly string[] AllTypes =
    [
        "movie",
        "tv",
        "artist",
        "album",
        "episode",
        "track",
        "season",
        "person",
        "collection",
        "image",
        "unknown",
    ];

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

    [Fact]
    public void Track_is_treated_as_a_main_entity()
    {
        PalettePriority.IsMain("track").Should().BeTrue();
    }

    [Fact]
    public void All_values_stay_within_the_small_zero_to_ten_scale()
    {
        foreach (string entityType in AllTypes)
        {
            PalettePriority.ForImport(entityType).Should().BeInRange(0, 10);
            PalettePriority.ForBackfill(entityType).Should().BeInRange(0, 10);
        }

        PalettePriority.OnDemand.Should().BeInRange(0, 10);
        PalettePriority.BackfillCoordinator.Should().BeInRange(0, 10);
    }

    [Fact]
    public void Every_import_outranks_every_backfill_regardless_of_entity_type()
    {
        foreach (string importType in AllTypes)
        foreach (string backfillType in AllTypes)
            PalettePriority
                .ForImport(importType)
                .Should()
                .BeGreaterThan(
                    PalettePriority.ForBackfill(backfillType),
                    $"any live import ({importType}) must outrank any backfill ({backfillType})"
                );
    }

    [Fact]
    public void On_demand_outranks_every_import_which_outranks_every_backfill_which_outranks_the_coordinator()
    {
        foreach (string entityType in AllTypes)
        {
            PalettePriority.OnDemand.Should().BeGreaterThan(PalettePriority.ForImport(entityType));
            PalettePriority
                .ForBackfill(entityType)
                .Should()
                .BeGreaterThan(PalettePriority.BackfillCoordinator);
        }
    }

    [Theory]
    [InlineData(["movie", "episode"])]
    [InlineData(["tv", "episode"])]
    [InlineData(["artist", "track"])]
    [InlineData(["album", "track"])]
    [InlineData(["episode", "season"])]
    [InlineData(["track", "season"])]
    [InlineData(["season", "person"])]
    [InlineData(["season", "collection"])]
    [InlineData(["season", "image"])]
    public void Entity_rank_hierarchy_holds_within_the_import_band(string higher, string lower)
    {
        PalettePriority
            .ForImport(higher)
            .Should()
            .BeGreaterThan(PalettePriority.ForImport(lower), $"{higher} outranks {lower}");
    }

    [Theory]
    [InlineData(["movie", "episode"])]
    [InlineData(["tv", "episode"])]
    [InlineData(["artist", "track"])]
    [InlineData(["album", "track"])]
    [InlineData(["episode", "season"])]
    [InlineData(["track", "season"])]
    [InlineData(["season", "person"])]
    [InlineData(["season", "collection"])]
    [InlineData(["season", "image"])]
    public void Entity_rank_hierarchy_holds_within_the_backfill_band(string higher, string lower)
    {
        PalettePriority
            .ForBackfill(higher)
            .Should()
            .BeGreaterThan(PalettePriority.ForBackfill(lower), $"{higher} outranks {lower}");
    }
}
