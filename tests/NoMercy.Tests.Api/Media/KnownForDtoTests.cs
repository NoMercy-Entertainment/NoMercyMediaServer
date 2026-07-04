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

using NoMercy.Api.DTOs.Media;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Pins <see cref="KnownForDto"/>'s "has item" detection against a null
/// navigation. <c>x?.Collection.Count != 0</c> is always true when <c>x</c> is
/// null (<c>null != 0</c>), so a cast/crew credit with no Movie must not be
/// misreported as having files.
/// </summary>
[Trait("Category", "Unit")]
public class KnownForDtoTests
{
    [Fact]
    public void Constructor_FromCastWithNullMovieAndEmptyEpisodes_ReportsNoItem()
    {
        Cast cast = new()
        {
            Role = new() { Character = "Herself" },
            Movie = null,
            Tv = new()
            {
                Id = 1,
                Title = "Test Show",
                Episodes =
                [
                    new()
                    {
                        Id = 1,
                        SeasonNumber = 1,
                        EpisodeNumber = 1,
                    },
                ],
            },
        };

        KnownForDto dto = new(cast);

        dto.HasItem.Should().BeFalse();
        dto.HaveItems.Should().Be(0);
    }
}
