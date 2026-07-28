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

using MovieFileLibrary;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Files.Parsing.Adapters;

namespace NoMercy.Tests.MediaProcessing.Parsing;

/// <summary>
/// What the file list actually produces for a name — the adapters plus the
/// folder-season and trailing-number rules that run around them.
/// <para>
/// Every name here is a real one, taken from a sweep of 11,539 files across the
/// library. Each group is a class of name that used to resolve to the wrong
/// place, and each group is followed by the names that must NOT move: these
/// rules read numbers out of release names, and a rule that is one character too
/// greedy quietly files a real episode as a special.
/// </para>
/// </summary>
public class FilenameResolverCorpusTests
{
    private static FilenameResolver Resolver() =>
        new(
            new FilenameParserPipeline(
                new IFilenameParseAdapter[]
                {
                    new EpisodePrefixAdapter(),
                    new EpisodeWordAdapter(),
                    new CrossFormatAdapter(),
                    new SeasonEpisodeAdapter(),
                    new SeasonSpecialAdapter(),
                    new AnimeAbsoluteAdapter(),
                    new EpisodeShortFormAdapter(),
                    new SpecialsAdapter(),
                    new MovieDetectorAdapter(),
                }
            )
        );

    private static MovieFile Resolve(string fileName, string directory = @"C:\Anime\Show") =>
        Resolver().Resolve(fileName, directory, Path.Combine(directory, fileName), "anime").Parsed;

    // ---------------------------------------------------------------------------
    // A re-release marks its version on the episode number: "- 01v2". The suffix
    // glues to the digits, so every matcher wanting a standalone number rejected
    // both halves and all 24 episodes of the show resolved to nothing at all.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(["[Sokudo] Dororo (2019) - 01v2 [1080p BD][AV1][dual audio].mkv", 1])]
    [InlineData(["[Sokudo] Dororo (2019) - 24v2 [1080p BD][AV1][dual audio].mkv", 24])]
    [InlineData(["Detective Conan - 678v2.mkv", 678])]
    public void A_version_suffix_does_not_hide_the_episode(string file, int episode)
    {
        MovieFile result = Resolve(file);
        result.Episode.Should().Be(episode);
    }

    [Fact]
    public void A_version_in_the_show_name_is_not_a_version_suffix()
    {
        // "Ver1.1a" is part of the title. Only a v bound to a preceding digit is
        // a release version, so the whole version string survives (dots become
        // spaces in every title, which is not this rule's doing).
        MovieFile result = Resolve("[Judas] NieR-Automata Ver1.1a - S01E18.mkv");
        result.Title.Should().Contain("Ver1 1a");
        result.Season.Should().Be(1);
        result.Episode.Should().Be(18);
    }

    // ---------------------------------------------------------------------------
    // Season-scoped specials carry a marker between the season and the number.
    // Each of these used to lose the marker and land on a REAL episode of season
    // one: six One Punch Man OVAs all became S01E01.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(["[Judas] One Punch Man - S01OVA05.mkv", 5])]
    [InlineData(["[Judas] Clannad - S02OVA02.mkv", 2])]
    [InlineData(["[Judas] Boku no Hero Academia - S07SP03v2.mkv", 3])]
    [InlineData(["[Judas] CHIHAYAFURU - S03SP01.mkv", 1])]
    [InlineData(["[Judas] Overlord - S02S13.mkv", 13])]
    public void A_season_scoped_special_is_season_zero(string file, int episode)
    {
        MovieFile result = Resolve(file);
        result.Season.Should().Be(0);
        result.Episode.Should().Be(episode);
    }

    // ---------------------------------------------------------------------------
    // A recap that airs between two episodes is numbered with a half, and was
    // read as the whole episode either side of it.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(["NANA - S01E21.5 (1080p VRV Dual Audio WEB-DL -KS-).mkv", 21])]
    [InlineData(["S02E05.5 [SP]-Guidepost [4227F7A0].mkv", 5])]
    [InlineData(["[Judas] NieR-Automata Ver1.1a - S01E18.5.mkv", 18])]
    public void A_half_episode_is_season_zero(string file, int episode)
    {
        MovieFile result = Resolve(file);
        result.Season.Should().Be(0);
        result.Episode.Should().Be(episode);
    }

    [Theory]
    // The guard on the rule above. A scene name separates with dots, so both a
    // resolution tag and an episode whose TITLE starts with a number sit exactly
    // where a half-episode would. "The Punisher - 3 A.M." is episode 1, not 1.5.
    [InlineData([
        "The.Punisher.S01E01.3.AM.2160p.DSNP.WEB-DL.DDP5.1.Atmos.HDR.H.265-SMURF.mkv",
        1,
        1,
    ])]
    [InlineData(["Breaking.Bad.S05E14.1080p.BluRay.x265-GROUP.mkv", 5, 14])]
    [InlineData(["NANA - S01E21 (1080p VRV Dual Audio WEB-DL -KS-).mkv", 1, 21])]
    [InlineData(["Moon.Knight.S01E06.Episode.6.2160p.WEB-DL.DDP5.1.Atmos.DV.MKV.x265.mkv", 1, 6])]
    public void A_dot_in_a_release_name_is_not_a_half_episode(string file, int season, int episode)
    {
        MovieFile result = Resolve(file);
        result.Season.Should().Be(season);
        result.Episode.Should().Be(episode);
    }

    // ---------------------------------------------------------------------------
    // A raw disc rip is named for its track. The track index is not an episode
    // and the only other number is the volume, so all 29 tracks of two discs were
    // claiming their volume's first episode.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("The Pink Panther Volume 1_t12.mkv")]
    [InlineData("The Pink Panther Volume 2_t06.mkv")]
    public void A_disc_track_guesses_no_episode(string file)
    {
        MovieFile result = Resolve(file, @"E:\TV.Shows\The Pink Panther Volume 1");
        result.Season.Should().BeNull();
        result.Episode.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Cutting the title at the release year split a parenthesised one in half, so
    // 524 files across nine shows searched the providers for "Fairy Tail (".
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(["[Erai-raws] Fairy Tail - 175 [1080p].mkv", "Fairy Tail"])]
    [InlineData(["[Sokudo] Dororo (2019) - 07v2 [1080p BD][AV1][dual audio].mkv", "Dororo"])]
    public void A_title_never_keeps_half_a_bracket(string file, string title)
    {
        Resolve(file).Title.Should().Be(title);
    }
}
