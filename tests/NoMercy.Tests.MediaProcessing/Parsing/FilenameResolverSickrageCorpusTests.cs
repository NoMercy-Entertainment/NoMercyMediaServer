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
/// The naming conventions SiCKRAGE recognised, as its own test corpus states
/// them.
/// <para>
/// SiCKRAGE (a SickBeard descendant) spent well over a decade accumulating the
/// shapes real releases actually take, and its parser ships with the corpus that
/// pins them. The project is no longer maintained, so the corpus is carried
/// forward here: every name below is one SiCKRAGE asserted on, run through this
/// resolver, and pinned to what this resolver must keep answering.
/// </para>
/// <para>
/// It is a specification of conventions, not a set of samples. Each group is one
/// convention — the scene's SxxExx, the cross-format NxNN, the fansub absolute
/// number, the daily air date — and a change that breaks a group has stopped
/// understanding a way people name television, which is a bigger claim than any
/// one file failing to import.
/// </para>
/// <para>
/// Where this resolver deliberately answers differently, the case is in
/// <see cref="A_convention_the_parser_deliberately_leaves_alone"/> with the
/// reason, rather than left out. A convention we decline to read out of the name
/// is worth pinning precisely because declining it was a decision.
/// </para>
/// </summary>
public class FilenameResolverSickrageCorpusTests
{
    private static FilenameResolver Resolver() =>
        new(
            new FilenameParserPipeline([
                new EpisodePrefixAdapter(),
                new EpisodeWordAdapter(),
                new CrossFormatAdapter(),
                new SeasonEpisodeAdapter(),
                new SeasonSpecialAdapter(),
                new AnimeAbsoluteAdapter(),
                new EpisodeShortFormAdapter(),
                new SpecialsAdapter(),
                new SeasonPackAdapter(),
                new PartAdapter(),
                new MovieDetectorAdapter(),
            ])
        );

    /// <summary>
    /// Runs a corpus name the way the file list does: as a real file, with its
    /// directory available for the folder rules. Names in the corpus that carry
    /// no extension are release names; every one of them reaches this parser as
    /// a file, so they are tested as one.
    /// </summary>
    private static readonly string[] VideoExtensions = [".avi", ".mkv", ".mp4", ".wmv", ".ext"];

    private static MovieFile Resolve(string corpusName)
    {
        string normalised = corpusName.Replace('\\', '/');

        // Not Path.HasExtension: "Show.Name.S01E02" has one by its reckoning,
        // and it is the marker. Only a real video extension counts.
        if (
            !VideoExtensions.Any(extension =>
                normalised.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            )
        )
            normalised += ".mkv";

        string directory = Path.GetDirectoryName(normalised) ?? "";
        return Resolver().Resolve(Path.GetFileName(normalised), directory, normalised, "tv").Parsed;
    }

    private static void Assert(string name, string? title, int? season, int? episode)
    {
        MovieFile result = Resolve(name);
        result.Title.Should().Be(title);
        result.Season.Should().Be(season);
        result.Episode.Should().Be(episode);
    }

    // -------------------------------------------------------------------------
    // The scene standard: SxxExx, with or without separators, repeated or
    // ranged. A joined multi-episode file resolves to the FIRST episode it
    // holds — it is not the second one, and filing it there leaves the first
    // reading as missing.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Mr.Show.Name.S01E02.Source.Quality.Etc-Group", "Mr Show Name", 1, 2)]
    [InlineData("Show.Name.S01E02", "Show Name", 1, 2)]
    [InlineData("Show Name - S01E02 - My Ep Name", "Show Name", 1, 2)]
    [InlineData("Show.1.0.Name.S01.E03.My.Ep.Name-Group", "Show 1 0 Name", 1, 3)]
    [InlineData("Show.Name.S01E02E03.Source.Quality.Etc-Group", "Show Name", 1, 2)]
    [InlineData("Mr. Show Name - S01E02-03 - My Ep Name", "Mr Show Name", 1, 2)]
    [InlineData("Show.Name.S01.E02.E03", "Show Name", 1, 2)]
    [InlineData("S01E02 Ep Name", null, 1, 2)]
    [InlineData("Show Name - S06E01 - -30-", "Show Name", 6, 1)]
    [InlineData("Show-Name-S06E01-720p", "Show-Name", 6, 1)]
    [InlineData("Show-Name-S06E01-1080i", "Show-Name", 6, 1)]
    [InlineData("Show.Name.S06E01.Other.WEB-DL", "Show Name", 6, 1)]
    [InlineData("Show.Name.S06E01 Some-Stuff Here", "Show Name", 6, 1)]
    [InlineData("Show.Name.S01E02.S01E03.Source.Quality.Etc-Group", "Show Name", 1, 2)]
    [InlineData("Show.Name.S01E02.S01E03", "Show Name", 1, 2)]
    [InlineData("Show Name - S01E02 - S01E03 - S01E04 - Ep Name", "Show Name", 1, 2)]
    [InlineData("Show.Name.S01E02.S01E03.WEB-DL", "Show Name", 1, 2)]
    public void The_season_and_episode_written_out(
        string name,
        string? title,
        int season,
        int episode
    ) => Assert(name, title, season, episode);

    /// <summary>
    /// A name carrying an explicit marker AND a date is not a daily episode.
    /// The date is printed in the title; the marker is the key. Reading the date
    /// as the key threw S06E01 away and the file arrived unmatched.
    /// </summary>
    [Fact]
    public void An_explicit_marker_outranks_a_date_in_the_same_name() =>
        Assert("Show Name - S06E01 - 2009-12-20 - Ep Name", "Show Name", 6, 1);

    // -------------------------------------------------------------------------
    // Cross-format numbering — 1x02 — including the bracketed spelling. The
    // brackets are the trap: everything a release puts in brackets is stripped
    // before a number is read, and "[05x12]" is the one bracket that holds the
    // answer rather than the noise.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Show_Name.1x02.Source_Quality_Etc-Group", "Show Name", 1, 2)]
    [InlineData("Show Name 1x02", "Show Name", 1, 2)]
    [InlineData("Show Name 1x02 x264 Test", "Show Name", 1, 2)]
    [InlineData("Show Name - 1x02 - My Ep Name", "Show Name", 1, 2)]
    [InlineData("Show_Name.1x02x03x04.Source_Quality_Etc-Group", "Show Name", 1, 2)]
    [InlineData("Show Name - 1x02-03-04 - My Ep Name", "Show Name", 1, 2)]
    [InlineData("1x02 Ep Name", null, 1, 2)]
    [InlineData("Show-Name-1x02-720p", "Show-Name", 1, 2)]
    [InlineData("Show-Name-1x02-1080i", "Show-Name", 1, 2)]
    [InlineData("Show Name [05x12] Ep Name", "Show Name", 5, 12)]
    [InlineData("Show.Name.1x02.WEB-DL", "Show Name", 1, 2)]
    [InlineData("Show.Name.1x02.1x03.Source.Quality.Etc-Group", "Show Name", 1, 2)]
    [InlineData("Show.Name.1x02.1x03", "Show Name", 1, 2)]
    [InlineData("Show Name - 1x02 - 1x03 - 1x04 - Ep Name", "Show Name", 1, 2)]
    [InlineData("Show.Name.1x02.1x03.WEB-DL", "Show Name", 1, 2)]
    public void Cross_format_numbering(string name, string? title, int season, int episode) =>
        Assert(name, title, season, episode);

    // -------------------------------------------------------------------------
    // No season in the name: a bare number, an E-prefix, or the word Episode.
    // The season comes from the folder, or defaults to one.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Show Name - 01 - Ep Name", "Show Name", 1, 1)]
    [InlineData("01 - Ep Name", null, 1, 1)]
    [InlineData("Show Name - 01 - Ep Name - WEB-DL", "Show Name", 1, 1)]
    [InlineData("Show.Name.E23.Source.Quality.Etc-Group", "Show Name", 1, 23)]
    [InlineData("Show Name - Episode 01 - Ep Name", "Show Name", 1, 1)]
    [InlineData("Deconstructed.E07.1080i.HDTV.DD5.1.MPEG2-TrollHD", "Deconstructed", 1, 7)]
    [InlineData("Show.Name.E23.WEB-DL", "Show Name", 1, 23)]
    [InlineData("Show.Name.E23-24.Source.Quality.Etc-Group", "Show Name", 1, 23)]
    [InlineData("Show Name - Episode 01-02 - Ep Name", "Show Name", 1, 1)]
    [InlineData("Show.Name.E23-24.WEB-DL", "Show Name", 1, 23)]
    public void No_season_in_the_name(string name, string? title, int season, int episode) =>
        Assert(name, title, season, episode);

    // -------------------------------------------------------------------------
    // Numbered by part rather than by episode, including roman numerals and the
    // "of N" total. The total is not a second number: reading it landed both
    // halves of a two-part release on the same episode, where one replaced the
    // other.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Show.Name.Part.3.Source.Quality.Etc-Group", "Show Name", 1, 3)]
    [InlineData("Show.Name.Part.1.and.Part.2.Blah-Group", "Show Name", 1, 1)]
    [InlineData("Show.Name.Part.IV.Source.Quality.Etc-Group", "Show Name", 1, 4)]
    [InlineData("Cleopatra_Part_1_of_2_-_(RAW).avi", "Cleopatra", 1, 1)]
    [InlineData("Cleopatra_Part_2_of_2_-_(RAW).avi", "Cleopatra", 1, 2)]
    public void Numbered_by_part(string name, string? title, int season, int episode) =>
        Assert(name, title, season, episode);

    // -------------------------------------------------------------------------
    // A name that says which season it belongs to and never says which episode.
    // It has no episode, and inventing one puts a season pack on top of a real
    // episode: "Show Name Season 2" was season one, episode two.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Show.Name.S02.Source.Quality.Etc-Group", "Show Name", 2)]
    [InlineData("Show Name Season 2", "Show Name", 2)]
    [InlineData("Season 02", null, 2)]
    public void A_season_with_no_episode(string name, string? title, int season) =>
        Assert(name, title, season, null);

    // -------------------------------------------------------------------------
    // Daily episodes, keyed by air date. The episode is whichever one aired that
    // day, so no season or episode number is guessed here.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Show.Name.2010.11.23.Source.Quality.Etc-Group", "Show Name")]
    [InlineData("Show Name - 2010.11.23", "Show Name")]
    [InlineData("Show Name - 2010-11-23 - Ep Name", "Show Name")]
    [InlineData("2010-11-23 - Ep Name", null)]
    [InlineData("Show.Name.2010.11.23.WEB-DL", "Show Name")]
    public void Dated_daily_episodes(string name, string? title) => Assert(name, title, null, null);

    // -------------------------------------------------------------------------
    // Paths, where the folder supplies what the file name leaves out. A
    // "Season N" folder names a season and not a show, so the show is the folder
    // above it — the standard layout, in which the file name is often nothing
    // but its marker.
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("/test/path/to/Season 02/03 - Ep Name.avi", "to", 2, 3)]
    [InlineData(
        "/home/drop/storage/TV/Terminator The Sarah Connor Chronicles/Season 2/S02E06 The Tower is Tall, But the Fall is Short.mkv",
        "Terminator The Sarah Connor Chronicles",
        2,
        6
    )]
    [InlineData("/X/30 Rock/Season 4/30 Rock - 4x22 -.avi", "30 Rock", 4, 22)]
    [InlineData("Season 2\\Show Name - 03-04 - Ep Name.ext", "Show Name", 2, 3)]
    [InlineData("Season 02\\03-04-05 - Ep Name.ext", null, 2, 3)]
    public void The_folder_supplies_what_the_name_leaves_out(
        string name,
        string? title,
        int season,
        int episode
    ) => Assert(name, title, season, episode);

    [Fact]
    public void A_season_folder_does_not_become_the_show_name() =>
        Assert(
            "/Test/TV/Jimmy Fallon/Season 2/Jimmy Fallon - 2010-12-15 - blah.avi",
            "Jimmy Fallon",
            null,
            null
        );

    /// <summary>
    /// SiCKRAGE's own declared failure case: a name that must NOT be parsed,
    /// because "jfcs01e09" is a release group's abbreviation with a marker glued
    /// to it rather than a show with a marker after it. Producing a confident
    /// season one episode nine here would file it against whatever show the
    /// abbreviation happened to search to.
    /// </summary>
    [Fact]
    public void A_marker_glued_into_a_release_group_is_not_an_episode() =>
        Assert("7sins-jfcs01e09-720p-bluray-x264", "7sins jfcs01e09", null, null);

    /// <summary>
    /// Non-ASCII survives the round trip. Both spellings are in the corpus — the
    /// second is the first read back through the wrong encoding, which is how it
    /// reaches a scanner when a release was packed on a differently configured
    /// machine.
    /// </summary>
    [Theory]
    [InlineData(
        "The.Big.Bang.Theory.2x07.The.Panty.Piñata.Polarization.720p.HDTV.x264.AC3-SHELDON.mkv"
    )]
    [InlineData(
        "The.Big.Bang.Theory.2x07.The.Panty.PiÃ±ata.Polarization.720p.HDTV.x264.AC3-SHELDON.mkv"
    )]
    public void Accented_and_mis_decoded_titles(string name) =>
        Assert(name, "The Big Bang Theory", 2, 7);

    /// <summary>
    /// A fullwidth digit is a digit to <c>\d</c> and not a digit to
    /// <c>int.Parse</c>. A real release — "First Season 「アタックオブ朹町２丁目…」" —
    /// matched on the fullwidth ２ and threw <c>FormatException</c> out of the
    /// scan, so one such file stopped an entire library import.
    /// </summary>
    [Theory]
    [InlineData("First Season 「アタックオブ朹町２丁目」 (NHK-G 1280ｘ720 x264 AAC).mkv")]
    [InlineData("【アニメDVD】ドラゴンボール 第039話 「謎の人造人間８号」 (VGA WMV9).wmv")]
    [InlineData("メイヴちゃん おまけ4[ストラトス４A告知].avi")]
    public void A_fullwidth_digit_does_not_throw(string name)
    {
        Action parse = () => Resolve(name);
        parse.Should().NotThrow();
    }

    /// <summary>
    /// Conventions SiCKRAGE reads out of the NAME that this parser deliberately
    /// leaves whole, each with the measurement that decided it.
    /// <para>
    /// <b>The bare scene number</b> — SiCKRAGE reads "Show.Name.102" as season
    /// one episode two and "the.event.401" as season four episode one. It can,
    /// because it asks its own show database whether the series is anime before
    /// applying the rule. Deciding it from the name alone was measured over
    /// 10,329 real release names: it changed 537 of them and a large share were
    /// wrong — "BLEACH - 154 SUB" became season one episode fifty-four,
    /// "One_Piece_310" became season three episode ten, and
    /// "第05話 「戦時特例法205号」" took the 205 out of the episode's own title.
    /// </para>
    /// <para>
    /// So the parser keeps the number whole and the split happens where the show
    /// is known, in the identification ladder, after every reading of it as one
    /// number has failed — see
    /// <c>MediaIdentificationService.ResolveSceneSplitEpisode</c> and
    /// <c>SceneNumberResolutionTests</c>. These cases pin what the PARSER
    /// answers, which is the whole number.
    /// </para>
    /// <para>
    /// <b>The release-group prefix</b> — "tpz-abc102" is group tpz, show abc,
    /// season one episode two. The shape appeared zero times in those 10,329
    /// names; files reach a library after being renamed, not as scene drops. The
    /// rule is all risk and no measured return.
    /// </para>
    /// <para>
    /// <b>The year inside the title</b> — SiCKRAGE keeps "Show Name-0 2010" as
    /// the show's name. Here the year is extracted separately and used to
    /// disambiguate the search, so leaving a copy in the title would only make
    /// the search string wrong.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Show.Name.102", "Show Name", 1, 102)]
    [InlineData("Show.Name.102.Source.Quality.Etc-Group", "Show Name", 1, 102)]
    [InlineData("show.ex-name.102.hdtv-group", "show ex-name", 1, 102)]
    [InlineData("show.name.2010.123.source.quality.etc-group", "show name", 1, 123)]
    [InlineData("the.event.401.hdtv-group", "the event", 1, 401)]
    [InlineData("tpz-abc.102", "tpz-abc", 1, 102)]
    [InlineData("Show.Name-0.2010.S01E02.Source.Quality.Etc-Group", "Show Name-0", 1, 2)]
    public void A_convention_the_parser_deliberately_leaves_alone(
        string name,
        string? title,
        int season,
        int episode
    ) => Assert(name, title, season, episode);
}
