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

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Corpus for <see cref="StringExtensions.TryGetFolderSeason"/>: season folders in
/// several conventions/languages resolve, while show folders, specials and titles
/// that merely contain a leading "S" never produce a spurious season.
/// </summary>
public class FolderSeasonTests
{
    [Theory]
    [InlineData(data: ["/media/Show Name/Season 2", 2])]
    [InlineData(data: ["Show/Season 02", 2])]
    [InlineData(data: ["/x/Season 0", 0])]
    [InlineData(data: ["/x/Season 2/", 2])]
    [InlineData(data: ["/x/S2", 2])]
    [InlineData(data: ["S02", 2])]
    [InlineData(data: ["/lib/Breaking Bad/Series 3", 3])]
    [InlineData(data: ["/x/Staffel 4", 4])]
    [InlineData(data: ["/x/Temporada 5", 5])]
    [InlineData(data: ["/x/Saison 6", 6])]
    [InlineData(data: ["/x/Stagione 7", 7])]
    public void Resolves_season_folders(string dir, int expected) =>
        dir.TryGetFolderSeason().Should().Be(expected: expected);

    [Theory]
    [InlineData(data: "/media/Show Name")]
    [InlineData(data: "/x/Specials")]
    [InlineData(data: "/x/Season of the Witch")]
    [InlineData(data: "/x/Season Finale")]
    [InlineData(data: "/x/Smallville")]
    [InlineData(data: "/x/Sherlock 2010")]
    [InlineData(data: "/x/S.W.A.T")]
    [InlineData(data: "")]
    [InlineData(data: null)]
    public void Rejects_non_season_folders(string? dir) =>
        dir.TryGetFolderSeason().Should().BeNull();
}
