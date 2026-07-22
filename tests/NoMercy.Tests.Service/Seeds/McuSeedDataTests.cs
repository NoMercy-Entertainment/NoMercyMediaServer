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

using FluentAssertions;
using NoMercy.NmSystem.Domain;
using NoMercy.Service.Seeds.Data;
using NoMercy.Service.Seeds.Dto;
using Xunit;

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// <see cref="SpecialSeed"/> (the opt-in Marvel dataset) trusts this curated
/// data to build a real Ulid-keyed Special row and to look up every item by
/// title/year/type via TMDB search. A malformed entry (empty title, an
/// unrecognized media type <see cref="SpecialSeed"/>'s switch silently drops)
/// would seed a broken or incomplete collection with no compiler feedback —
/// these tests pin the shape SpecialSeed actually depends on.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class McuSeedDataTests
{
    [Fact]
    public void Special_HasAStableNonEmptyId()
    {
        McuSeedData.Special.Id.Should().NotBe(unexpected: Ulid.Empty);
    }

    [Fact]
    public void Special_HasTitleAndArtwork()
    {
        McuSeedData.Special.Title.Should().NotBeNullOrWhiteSpace();
        McuSeedData.Special.Backdrop.Should().NotBeNullOrWhiteSpace();
        McuSeedData.Special.Poster.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void McuItems_IsNotEmpty()
    {
        McuSeedData.McuItems.Should().NotBeEmpty();
    }

    [Fact]
    public void McuItems_EveryEntryHasTitleYearAndKnownType()
    {
        string[] knownTypes =
        [
            MediaTypes.MovieMediaType,
            MediaTypes.TvMediaType,
            MediaTypes.AnimeMediaType,
        ];

        foreach (SpecialItem item in McuSeedData.McuItems)
        {
            item.Title.Should().NotBeNullOrWhiteSpace();
            item.Year.Should().BeGreaterThan(expected: 1900);
            knownTypes.Should().Contain(expected: item.Type);
        }
    }

    [Fact]
    public void McuItems_IndicesAreUniqueAndSequential()
    {
        int[] indices = McuSeedData.McuItems.Select(selector: item => item.Index).ToArray();

        indices.Should().OnlyHaveUniqueItems();
    }
}
