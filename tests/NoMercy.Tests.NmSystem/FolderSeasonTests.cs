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
    [InlineData(["/media/Show Name/Season 2", 2])]
    [InlineData(["Show/Season 02", 2])]
    [InlineData(["/x/Season 0", 0])]
    [InlineData(["/x/Season 2/", 2])]
    [InlineData(["/x/S2", 2])]
    [InlineData(["S02", 2])]
    [InlineData(["/lib/Breaking Bad/Series 3", 3])]
    [InlineData(["/x/Staffel 4", 4])]
    [InlineData(["/x/Temporada 5", 5])]
    [InlineData(["/x/Saison 6", 6])]
    [InlineData(["/x/Stagione 7", 7])]
    public void Resolves_season_folders(string dir, int expected) =>
        dir.TryGetFolderSeason().Should().Be(expected);

    [Theory]
    [InlineData("/media/Show Name")]
    [InlineData("/x/Specials")]
    [InlineData("/x/Season of the Witch")]
    [InlineData("/x/Season Finale")]
    [InlineData("/x/Smallville")]
    [InlineData("/x/Sherlock 2010")]
    [InlineData("/x/S.W.A.T")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_non_season_folders(string? dir) =>
        dir.TryGetFolderSeason().Should().BeNull();
}
