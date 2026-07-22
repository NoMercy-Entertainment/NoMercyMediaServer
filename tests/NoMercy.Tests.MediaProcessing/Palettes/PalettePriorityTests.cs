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
            .ForImport(entityType: "episode")
            .Should()
            .BeGreaterThan(
                expected: PalettePriority.ForImport(entityType: "image"),
                because: "a live import must paint the episode before its images"
            );

        foreach (string main in new[] { "movie", "tv", "season", "episode", "artist", "album" })
            PalettePriority
                .ForImport(entityType: main)
                .Should()
                .BeGreaterThan(expected: PalettePriority.ForImport(entityType: "image"), because: $"{main} outranks its images");
    }

    [Fact]
    public void Tiers_drain_in_order_on_demand_import_backfill_coordinator()
    {
        PalettePriority.OnDemand.Should().BeGreaterThan(expected: PalettePriority.ForImport(entityType: "episode"));
        PalettePriority
            .ForImport(entityType: "episode")
            .Should()
            .BeGreaterThan(expected: PalettePriority.ForImport(entityType: "image"));
        PalettePriority
            .ForImport(entityType: "image")
            .Should()
            .BeGreaterThan(
                expected: PalettePriority.ForBackfill(entityType: "episode"),
                because: "a live import outranks any backfill"
            );
        PalettePriority
            .ForBackfill(entityType: "episode")
            .Should()
            .BeGreaterThan(expected: PalettePriority.ForBackfill(entityType: "image"));
        PalettePriority
            .ForBackfill(entityType: "image")
            .Should()
            .BeGreaterThan(
                expected: PalettePriority.BackfillCoordinator,
                because: "the coordinator yields to the jobs it dispatches"
            );
    }

    [Fact]
    public void Image_is_not_treated_as_a_main_entity()
    {
        PalettePriority.IsMain(entityType: "image").Should().BeFalse();
        PalettePriority.IsMain(entityType: "episode").Should().BeTrue();
    }

    [Fact]
    public void Track_is_treated_as_a_main_entity()
    {
        PalettePriority.IsMain(entityType: "track").Should().BeTrue();
    }

    [Fact]
    public void All_values_stay_within_the_small_zero_to_ten_scale()
    {
        foreach (string entityType in AllTypes)
        {
            PalettePriority.ForImport(entityType: entityType).Should().BeInRange(minimumValue: 0, maximumValue: 10);
            PalettePriority.ForBackfill(entityType: entityType).Should().BeInRange(minimumValue: 0, maximumValue: 10);
        }

        PalettePriority.OnDemand.Should().BeInRange(minimumValue: 0, maximumValue: 10);
        PalettePriority.BackfillCoordinator.Should().BeInRange(minimumValue: 0, maximumValue: 10);
    }

    [Fact]
    public void Every_import_outranks_every_backfill_regardless_of_entity_type()
    {
        foreach (string importType in AllTypes)
        foreach (string backfillType in AllTypes)
            PalettePriority
                .ForImport(entityType: importType)
                .Should()
                .BeGreaterThan(
                    expected: PalettePriority.ForBackfill(entityType: backfillType),
                    because: $"any live import ({importType}) must outrank any backfill ({backfillType})"
                );
    }

    [Fact]
    public void On_demand_outranks_every_import_which_outranks_every_backfill_which_outranks_the_coordinator()
    {
        foreach (string entityType in AllTypes)
        {
            PalettePriority.OnDemand.Should().BeGreaterThan(expected: PalettePriority.ForImport(entityType: entityType));
            PalettePriority
                .ForBackfill(entityType: entityType)
                .Should()
                .BeGreaterThan(expected: PalettePriority.BackfillCoordinator);
        }
    }

    [Theory]
    [InlineData(data: ["movie", "episode"])]
    [InlineData(data: ["tv", "episode"])]
    [InlineData(data: ["artist", "track"])]
    [InlineData(data: ["album", "track"])]
    [InlineData(data: ["episode", "season"])]
    [InlineData(data: ["track", "season"])]
    [InlineData(data: ["season", "person"])]
    [InlineData(data: ["season", "collection"])]
    [InlineData(data: ["season", "image"])]
    public void Entity_rank_hierarchy_holds_within_the_import_band(string higher, string lower)
    {
        PalettePriority
            .ForImport(entityType: higher)
            .Should()
            .BeGreaterThan(expected: PalettePriority.ForImport(entityType: lower), because: $"{higher} outranks {lower}");
    }

    [Theory]
    [InlineData(data: ["movie", "episode"])]
    [InlineData(data: ["tv", "episode"])]
    [InlineData(data: ["artist", "track"])]
    [InlineData(data: ["album", "track"])]
    [InlineData(data: ["episode", "season"])]
    [InlineData(data: ["track", "season"])]
    [InlineData(data: ["season", "person"])]
    [InlineData(data: ["season", "collection"])]
    [InlineData(data: ["season", "image"])]
    public void Entity_rank_hierarchy_holds_within_the_backfill_band(string higher, string lower)
    {
        PalettePriority
            .ForBackfill(entityType: higher)
            .Should()
            .BeGreaterThan(expected: PalettePriority.ForBackfill(entityType: lower), because: $"{higher} outranks {lower}");
    }
}
