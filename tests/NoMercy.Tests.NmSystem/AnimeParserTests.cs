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

using NoMercy.NmSystem.Dto;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins <see cref="AnimeParser.ParseAnimeFilename"/>: the fansub-style filename
/// parser that pulls group, name, season, episode, checksum and extension out of
/// bracketed anime release names, and falls back cleanly when nothing matches.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class AnimeParserTests
{
    [Fact]
    public void ParseAnimeFilename_ExtractsEpisodeRelease()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename(
            filename: "[HorribleSubs] Some Show - 01 [1080p][ABCD1234].mkv"
        );

        info.Group.Should().Be(expected: "HorribleSubs");
        info.Name.Trim().Should().Be(expected: "Some Show");
        info.Season.Should().BeNull();
        info.Episode.Should().Be(expected: 1);
        info.Checksum.Should().Be(expected: "ABCD1234");
        info.Extension.Should().Be(expected: "mkv");
    }

    [Fact]
    public void ParseAnimeFilename_ExtractsSeasonThenEpisode()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename(filename: "[Grp] Show S2 - 05 [DEADBEEF].mkv");

        info.Name.Trim().Should().Be(expected: "Show");
        info.Season.Should().Be(expected: 2);
        info.Episode.Should().Be(expected: 5);
        info.Checksum.Should().Be(expected: "DEADBEEF");
    }

    [Fact]
    public void ParseAnimeFilename_StripsVersionSuffix()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename(filename: "[Grp] Show - 05v2 [DEADBEEF].mkv");

        info.Episode.Should().Be(expected: 5);
        info.Season.Should().BeNull();
    }

    [Fact]
    public void ParseAnimeFilename_UsesUnderscoresAsSeparators()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename(filename: "[Grp]_Show_-_07_[ABCDEF12].mkv");

        info.Name.Trim().Should().Be(expected: "Show");
        info.Episode.Should().Be(expected: 7);
    }

    [Fact]
    public void ParseAnimeFilename_NoMatchReturnsFileNameOnly()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename(filename: "random video.mp4");

        info.FileName.Should().Be(expected: "random video.mp4");
        info.Group.Should().BeEmpty();
        info.Season.Should().BeNull();
        info.Episode.Should().BeNull();
        info.Checksum.Should().BeNull();
    }
}
