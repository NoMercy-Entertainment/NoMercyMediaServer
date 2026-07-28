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
/// Every name here is a real one, taken from a sweep of the 11,539 video paths
/// in the encode-campaign ledger — an inventory of source files, most of which
/// are not in a library. It is a corpus of real-world naming, not a statement
/// about the library's contents, and only the NAMES were run: no file was read
/// and no provider was asked.
/// </para>
/// <para>
/// Each group is a class of name that used to resolve to the wrong
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

    // ---------------------------------------------------------------------------
    // A file named for its ROLE in a release has no title in it, and the show it
    // belongs to is the folder holding it. Searching a provider for the role word
    // instead is how a creditless ending became an episode of a romance series
    // and a disc menu became an episode of a cooking show — a search always
    // returns something, which is why it must not be asked.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("Opening 06.mkv")] // matched "Opening Act" S01E06
    [InlineData("Ending 18.mkv")] // matched "Never-Ending Summer" S01E19
    [InlineData("Menu - 01.mkv")] // matched "Great British Menu" S01E01
    [InlineData("NCED - 01.mkv")] // matched "Flavor, NC" S01E01
    [InlineData("NCOP.mkv")]
    [InlineData("Creditless Opening 2.mkv")]
    [InlineData("- 03 -.mkv")]
    public void A_file_named_for_its_role_takes_the_title_from_its_folder(string file)
    {
        MovieFile result = Resolve(file, @"E:\Anime\Bocchi the Rock!");

        result
            .Title.Should()
            .Be(
                "Bocchi the Rock!",
                "the show is the folder — the name only says what the file is inside the release"
            );
    }

    [Fact]
    public void A_role_word_with_no_folder_to_fall_back_on_stays_unmatched()
    {
        // Nobody knows what this is. An unmatched file is one the operator can
        // place; a confident wrong answer is one they have to find first.
        Resolve("Opening 06.mkv", string.Empty).Title.Should().BeNull();
    }

    [Theory]
    // The guard. These are real shows whose names ARE those words, and a file
    // that names its show keeps it. The rule only fires when the role word is
    // the whole of what was found.
    [InlineData(["Opening Act - S01E06 - Tanner & Brad Paisley.mkv", "Opening Act"])]
    [InlineData(["Great British Menu - S07E02 - Scotland Fish.mkv", "Great British Menu"])]
    [InlineData(["Never-Ending Summer - S01E19 - Episode 19.mkv", "Never-Ending Summer"])]
    [InlineData(["Special A - S01E01 - Hello.mkv", "Special A"])]
    public void A_show_whose_name_contains_a_role_word_keeps_its_name(string file, string title)
    {
        Resolve(file, @"E:\TV.Shows\Whatever").Title.Should().Be(title);
    }

    // ---------------------------------------------------------------------------
    // Everything below came out of a sweep of 10,329 real release names — the
    // archived nyaa.se listings for torrent ids 1–10,000. These are not invented
    // shapes; each one is a file somebody actually released, and each was
    // resolved wrongly before the rule beneath it existed.
    // ---------------------------------------------------------------------------

    [Theory]
    // The technical block a release puts in parentheses is full of digits, and
    // only square brackets were ever stripped before an adapter read a number.
    // So the codec's own version became the episode: DivX6.61 -> episode 61,
    // DivX6.7 -> episode 7. The real number was in plain sight before "RAW".
    [InlineData(["H2O ~FOOTPRINTS IN THE SAND~ 03 RAW (1280x720 DivX6.61).avi", 3])]
    [InlineData(["Hatenkou Yuugi 03 RAW (D-KBS_704x396 24fps DivX6.7).avi", 3])]
    [InlineData(["CLANNAD 06 raw(BS-i DivX6.6 1280x720).avi", 6])]
    public void A_codec_version_is_not_an_episode_number(string file, int episode)
    {
        Resolve(file).Episode.Should().Be(episode);
    }

    [Theory]
    // What the same three files should be CALLED. A title carrying the source,
    // the codec or the resolution is a search string no catalogue holds.
    [InlineData([
        "H2O ~FOOTPRINTS IN THE SAND~ 03 RAW (1280x720 DivX6.61).avi",
        "H2O ~FOOTPRINTS IN THE SAND~",
    ])]
    [InlineData(["Hatenkou Yuugi 03 RAW (D-KBS_704x396 24fps DivX6.7).avi", "Hatenkou Yuugi"])]
    [InlineData(["CLANNAD 06 raw(BS-i DivX6.6 1280x720).avi", "CLANNAD"])]
    public void A_title_does_not_keep_the_release_it_came_from(string file, string title)
    {
        Resolve(file).Title.Should().Be(title);
    }

    [Fact]
    public void A_bracket_with_no_opening_half_does_not_ride_into_the_title()
    {
        // The group opens before the file name starts, so nothing pairs with the
        // "]" and the paired-group strip cannot see it. Searched for as
        // "AW-Raw] One Piece".
        MovieFile result = Resolve("AW-Raw]_One_Piece_310_SD.WS_(704x396)[6AF865BD].avi");

        result.Title.Should().Be("One Piece");
        result.Episode.Should().Be(310);
    }

    [Fact]
    public void A_creditless_marker_ends_the_title_rather_than_joining_it()
    {
        // The show IS named here, unlike the bare "NCED - 01" shape — it is just
        // followed by what the file is. Everything from the marker on describes
        // the extra, not the show.
        Resolve("Air TV - Creditless Opening (DVD)(AT).avi").Title.Should().Be("Air TV");
    }

    [Fact]
    public void A_name_that_is_only_punctuation_falls_back_to_the_folder()
    {
        // A directory listing's tree drawing, kept as a file name. The show's
        // real name appears only inside brackets, and every bracket is stripped,
        // so "│" was the entire title on 41 files in one archive.
        Resolve(
            "│      [00][SG][ARIA_The_Animation][00][XVID][BIG5].avi",
            @"E:\Anime\ARIA The Animation"
        )
            .Title.Should()
            .Be("ARIA The Animation");
    }
}
