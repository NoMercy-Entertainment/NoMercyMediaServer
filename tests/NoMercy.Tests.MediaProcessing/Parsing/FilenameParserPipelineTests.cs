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
using NoMercy.NmSystem.Extensions;
using NoMercy.Tests.Common.TestMedia;

namespace NoMercy.Tests.MediaProcessing.Parsing;

/// <summary>
/// Corpus-driven tests for the filename parse pipeline. Every adapter is backed by
/// many real scene/western/anime/movie names, and the ordering tests prove no
/// adapter under-classifies (claims a name a more-specific adapter should own).
/// </summary>
public class FilenameParserPipelineTests
{
    private static IFilenameParseAdapter[] DefaultAdapters() =>
        new IFilenameParseAdapter[]
        {
            new EpisodePrefixAdapter(),
            new EpisodeWordAdapter(),
            new CrossFormatAdapter(),
            new SeasonEpisodeAdapter(),
            new AnimeAbsoluteAdapter(),
            new EpisodeShortFormAdapter(),
            new SpecialsAdapter(),
            new MovieDetectorAdapter(),
        };

    private static IFilenameParserPipeline Pipeline() =>
        new FilenameParserPipeline(adapters: DefaultAdapters());

    private static ParseContext Context(
        string fileName,
        string folderTitle = "",
        string libraryType = "tv"
    )
    {
        string cleaned = StringExtensions
            .RemoveBracketedString()
            .Replace(input: Path.GetFileNameWithoutExtension(path: fileName), replacement: string.Empty)
            .Trim();

        return new()
        {
            FileNameWithExtension = fileName,
            DirectoryName = null,
            Title = StringExtensions.RemoveBracketedString().Replace(input: fileName, replacement: string.Empty),
            CleanedFileName = cleaned,
            FolderTitle = folderTitle,
            LibraryType = libraryType,
        };
    }

    /// <summary>The first adapter (in effective order) that returns a result.</summary>
    private static string? FirstMatchingAdapter(ParseContext context)
    {
        foreach (IFilenameParseAdapter adapter in DefaultAdapters().OrderBy(keySelector: a => a.Order))
            if (adapter.TryParse(context: context) is not null)
                return adapter.Name;
        return null;
    }

    // ---------------------------------------------------------------------------
    // SxxExx anywhere (the bread-and-butter scene format)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["One.Piece.S01E1109.1080p.WEB-DL.mkv", 1, 1109])]
    [InlineData(data: ["The.Office.US.S03E12.720p.HDTV.x264-LOL.mkv", 3, 12])]
    [InlineData(data: ["Breaking.Bad.S05E14.1080p.BluRay.x265-GROUP.mkv", 5, 14])]
    [InlineData(data: ["Game of Thrones - S08E06 - The Iron Throne.mkv", 8, 6])]
    [InlineData(data: ["Mr_Robot_S02E05_720p.mkv", 2, 5])]
    [InlineData(data: ["Stranger.Things.S04E09.2160p.NF.WEB-DL.DDP5.1.x265-NTb.mkv", 4, 9])]
    [InlineData(data: ["Better.Call.Saul.S06E13.PROPER.1080p.mkv", 6, 13])]
    [InlineData(data: ["Rick and Morty S05E10 1080p.mkv", 5, 10])]
    [InlineData(data: ["Dark.S01E01.German.DL.1080p.BluRay.x264-GROUP.mkv", 1, 1])]
    [InlineData(data: ["The.Mandalorian.S02E08.2160p.WEB-DL.mkv", 2, 8])]
    [InlineData(data: ["Severance.S01E09.1080p.ATVP.WEBRip.mkv", 1, 9])]
    [InlineData(data: ["Show.Name.s1e3.mkv", 1, 3])]
    public void SeasonEpisode_anywhere_resolves(string file, int season, int episode)
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: Context(fileName: file)).Should().Be(expected: "season-episode");
    }

    // ---------------------------------------------------------------------------
    // SxxExx at the very start (title comes from the folder)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["S01E05 - The Title.mkv", 1, 5])]
    [InlineData(data: ["S03E10.1080p.mkv", 3, 10])]
    [InlineData(data: ["s1e1.mkv", 1, 1])]
    [InlineData(data: ["S12E24-Finale.mkv", 12, 24])]
    public void EpisodePrefix_at_start_uses_folder_title(string file, int season, int episode)
    {
        const string folder = "The Show Name";
        MovieFile result = Pipeline().Parse(context: Context(fileName: file, folderTitle: folder));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
        result.Title.Should().Be(expected: folder);
        FirstMatchingAdapter(context: Context(fileName: file, folderTitle: folder)).Should().Be(expected: "episode-prefix");
    }

    // ---------------------------------------------------------------------------
    // "Episode NN" word form (separator required before the word)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["Blade - Episode 02 - title.mp4", 2])]
    [InlineData(data: ["Naruto Episode 130.mkv", 130])]
    [InlineData(data: ["Cowboy Bebop - Episode 5.mkv", 5])]
    [InlineData(data: ["Some.Show.Episode12.mkv", 12])]
    public void EpisodeWord_resolves(string file, int episode)
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(expected: 1);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: Context(fileName: file)).Should().Be(expected: "episode-word");
    }

    // ---------------------------------------------------------------------------
    // Movies must NOT be under-classified as episodes by any series adapter.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: "Inception.2010.1080p.BluRay.x264-GROUP.mkv")]
    [InlineData(data: "The.Matrix.1999.REMASTERED.1080p.BluRay.x265.mkv")]
    [InlineData(data: "Dune.Part.Two.2024.2160p.WEB-DL.DDP5.1.x265-FLUX.mkv")]
    [InlineData(data: "Avengers.Endgame.2019.UHD.BluRay.2160p.mkv")]
    [InlineData(data: "Blade Runner 2049 (2017).mkv")]
    [InlineData(data: "Top.Gun.Maverick.2022.IMAX.1080p.mkv")]
    public void Movies_are_not_classified_as_episodes(string file)
    {
        ParseContext context = Context(fileName: file, libraryType: "movie");
        new EpisodePrefixAdapter().TryParse(context: context).Should().BeNull();
        new EpisodeWordAdapter().TryParse(context: context).Should().BeNull();
        new SeasonEpisodeAdapter().TryParse(context: context).Should().BeNull();
        new CrossFormatAdapter().TryParse(context: context).Should().BeNull();
        new AnimeAbsoluteAdapter().TryParse(context: context).Should().BeNull();
        FirstMatchingAdapter(context: context).Should().Be(expected: "movie-detector");
    }

    // ---------------------------------------------------------------------------
    // Ordering / no under-classification: the FIRST adapter to match must be the
    // most specific correct one.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["One.Piece.S01E1109.mkv", "season-episode"])]
    [InlineData(data: ["Blade - Episode 02.mkv", "episode-word"])]
    [InlineData(data: ["Inception.2010.1080p.mkv", "movie-detector"])]
    public void Pipeline_first_match_is_expected_adapter(string file, string expected) =>
        FirstMatchingAdapter(context: Context(fileName: file, folderTitle: "Folder Title")).Should().Be(expected: expected);

    [Fact]
    public void EpisodePrefix_at_start_wins_over_season_episode_for_same_name()
    {
        // "S01E05..." matches the start-anchored prefix; the .SxxExx (separator
        // required) form deliberately does NOT match a leading token, so prefix owns it.
        ParseContext context = Context(fileName: "S01E05.mkv", folderTitle: "My Show");
        new EpisodePrefixAdapter().TryParse(context: context).Should().NotBeNull();
        new SeasonEpisodeAdapter().TryParse(context: context).Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Raw regex contracts (positive + negative) so each pattern is pinned down.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["S01E01", true])]
    [InlineData(data: ["s1e1", true])]
    [InlineData(data: ["S01E1109", true])]
    [InlineData(data: ["S12E100", true])]
    [InlineData(data: ["Show.S01E01", false])]
    [InlineData(data: ["Movie.2010.1080p", false])]
    [InlineData(data: ["1x01", false])]
    public void MatchEpisodePrefix_contract(string input, bool expected) =>
        StringExtensions.MatchEpisodePrefix().IsMatch(input: input).Should().Be(expected: expected);

    [Theory]
    [InlineData(data: ["Show.S01E01", true])]
    [InlineData(data: ["Show S01E01", true])]
    [InlineData(data: ["Show-S01E01", true])]
    [InlineData(data: ["Show_S02E05", true])]
    [InlineData(data: ["S01E01", false])]
    [InlineData(data: ["ShowS01E01", false])]
    [InlineData(data: ["Show.2010.1080p", false])]
    public void MatchSeasonEpisode_contract(string input, bool expected) =>
        StringExtensions.MatchSeasonEpisode().IsMatch(input: input).Should().Be(expected: expected);

    [Theory]
    [InlineData(data: [" Episode 5", true])]
    [InlineData(data: ["- Episode 02", true])]
    [InlineData(data: [".Episode12", true])]
    [InlineData(data: ["Show Episode 130", true])]
    [InlineData(data: ["Episode 5", false])]
    [InlineData(data: ["Show.Episode.5", false])]
    public void MatchEpisodeWord_contract(string input, bool expected) =>
        StringExtensions.MatchEpisodeWord().IsMatch(input: input).Should().Be(expected: expected);

    [Theory]
    [InlineData(data: ["Movie 2010", true])]
    [InlineData(data: ["Show 1999", true])]
    [InlineData(data: ["Film 2024", true])]
    [InlineData(data: ["Video 1080p", false])]
    [InlineData(data: ["Clip 2160p", false])]
    [InlineData(data: ["Thing 12345", false])]
    public void MatchYearRegex_contract(string input, bool expected) =>
        StringExtensions.MatchYearRegex().IsMatch(input: input).Should().Be(expected: expected);

    // ---------------------------------------------------------------------------
    // Pipeline configurability: disable + reorder via FilenameParsingOptions.
    // ---------------------------------------------------------------------------
    [Fact]
    public void Disabling_an_adapter_removes_it_from_the_pipeline()
    {
        FilenameParsingOptions options = new() { Disabled = { "season-episode" } };
        FilenameParserPipeline pipeline = new(adapters: DefaultAdapters(), options: options);
        pipeline.Order.Should().NotContain(unexpected: "season-episode");
        pipeline.Order.Should().Contain(expected: "movie-detector");
    }

    [Fact]
    public void Order_override_runs_named_adapter_first()
    {
        FilenameParsingOptions options = new() { Order = { "movie-detector" } };
        FilenameParserPipeline pipeline = new(adapters: DefaultAdapters(), options: options);
        pipeline.Order.First().Should().Be(expected: "movie-detector");
    }

    // ---------------------------------------------------------------------------
    // Cross-format "1x05" (and unicode "1×05"). Resolution like 1920x1080 must
    // NOT match (digits before the separator are preceded by a digit).
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["Battlestar.Galactica.3x20.1080p.mkv", 3, 20])]
    [InlineData(data: ["Show Name 1x05.mkv", 1, 5])]
    [InlineData(data: ["Series.12x08.720p.mkv", 12, 8])]
    [InlineData(data: ["Farscape - 1×01 - Pilot.mkv", 1, 1])]
    public void CrossFormat_resolves(string file, int season, int episode)
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: Context(fileName: file)).Should().Be(expected: "cross-format");
    }

    [Theory]
    [InlineData(data: "Show.Name.1920x1080.SBS.mkv")]
    [InlineData(data: "Clip.1280x720.mkv")]
    public void CrossFormat_ignores_resolution(string file) =>
        new CrossFormatAdapter().TryParse(context: Context(fileName: file)).Should().BeNull();

    // ---------------------------------------------------------------------------
    // Anime absolute numbering (anime/TV libraries only).
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["One Piece - 1109.mkv", 1109])]
    [InlineData(data: ["Naruto Shippuuden - 500.mkv", 500])]
    [InlineData(data: ["Bleach - 366 [1080p].mkv", 366])]
    [InlineData(data: ["Fairy Tail 175.mkv", 175])]
    public void AnimeAbsolute_resolves_for_anime_library(string file, int absolute)
    {
        ParseContext context = Context(fileName: file, libraryType: "anime");
        MovieFile result = Pipeline().Parse(context: context);
        result.IsSeries.Should().BeTrue();
        result.Episode.Should().Be(expected: absolute);
        FirstMatchingAdapter(context: context).Should().Be(expected: "anime-absolute");
    }

    [Theory]
    [InlineData(data: ["Firefly - 2002.mkv", "anime"])]
    [InlineData(data: ["One Piece - 1109.mkv", "movie"])]
    [InlineData(data: ["Inception 2010.mkv", "tv"])]
    public void AnimeAbsolute_does_not_fire(string file, string library) =>
        new AnimeAbsoluteAdapter().TryParse(context: Context(fileName: file, libraryType: library)).Should().BeNull();

    [Fact]
    public void Explicit_SxxExx_wins_over_anime_absolute_in_anime_library()
    {
        ParseContext context = Context(fileName: "Attack on Titan S04E28 1080p.mkv", libraryType: "anime");
        FirstMatchingAdapter(context: context).Should().Be(expected: "season-episode");
        MovieFile result = Pipeline().Parse(context: context);
        result.Season.Should().Be(expected: 4);
        result.Episode.Should().Be(expected: 28);
    }

    // ---------------------------------------------------------------------------
    // Scene-tag title cleanup: derived titles are stripped of quality/source/codec
    // noise, while real titles that resemble tag substrings survive intact.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["Some Show 1080p WEB-DL - Episode 05.mkv", "Some Show", 5])]
    [InlineData(data: ["Another.Show.720p.HDTV - Episode 12.mp4", "Another Show", 12])]
    public void Title_cleanup_episode_word(string file, string title, int episode)
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.IsSeries.Should().BeTrue();
        result.Title.Should().Be(expected: title);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: Context(fileName: file)).Should().Be(expected: "episode-word");
    }

    [Theory]
    [InlineData(data: ["Some Show 1080p - 12.mkv", "Some Show", 12])]
    public void Title_cleanup_anime_absolute(string file, string title, int episode)
    {
        ParseContext context = Context(fileName: file, libraryType: "anime");
        MovieFile result = Pipeline().Parse(context: context);
        result.IsSeries.Should().BeTrue();
        result.Title.Should().Be(expected: title);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: context).Should().Be(expected: "anime-absolute");
    }

    [Theory]
    [InlineData(data: ["Reacher.S01E01.1080p.WEB-DL.mkv", "Reacher", 1, 1])]
    [InlineData(data: ["Limitless.S01E05.720p.HDTV.x264.mkv", "Limitless", 1, 5])]
    [InlineData(data: ["Breaking.Bad.S05E14.1080p.BluRay.x265-GROUP.mkv", "Breaking Bad", 5, 14])]
    public void Title_cleanup_preserves_real_titles_in_season_episode(
        string file,
        string title,
        int season,
        int episode
    )
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.IsSeries.Should().BeTrue();
        result.Title.Should().Be(expected: title);
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: Context(fileName: file)).Should().Be(expected: "season-episode");
    }

    // ---------------------------------------------------------------------------
    // Short episode forms (E05 / Ep5 / S01.E05) that omit the contiguous SxxExx.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["Naruto.E12.1080p.WEB-DL.mkv", "Naruto", 1, 12])]
    [InlineData(data: ["Show Name Ep5.mkv", "Show Name", 1, 5])]
    [InlineData(data: ["Show.Name.Ep.05.720p.mkv", "Show Name", 1, 5])]
    [InlineData(data: ["Show.Name.S02.E07.mkv", "Show Name", 2, 7])]
    public void Episode_short_form(string file, string title, int season, int episode)
    {
        ParseContext context = Context(fileName: file);
        MovieFile result = Pipeline().Parse(context: context);
        result.IsSeries.Should().BeTrue();
        result.Title.Should().Be(expected: title);
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: context).Should().Be(expected: "episode-short-form");
    }

    [Theory]
    [InlineData(data: "Resident Evil 2.mkv")]
    [InlineData(data: "iPhone5.review.mkv")]
    [InlineData(data: "Se7en.1995.mkv")]
    [InlineData(data: "Route.66.1080p.mkv")]
    public void Episode_short_form_does_not_over_classify(string file) =>
        new EpisodeShortFormAdapter().TryParse(context: Context(fileName: file)).Should().BeNull();

    [Fact]
    public void Episode_short_form_is_skipped_for_movie_libraries() =>
        new EpisodeShortFormAdapter()
            .TryParse(context: Context(fileName: "Show.E05.mkv", libraryType: "movie"))
            .Should()
            .BeNull();

    // ---------------------------------------------------------------------------
    // Specials / season-zero: labelled (OVA/SP/NCED/Special) rather than numbered.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["One Piece - OVA 3.mkv", "anime", "One Piece", 3])]
    [InlineData(data: ["Bleach.SP01.mkv", "tv", "Bleach", 1])]
    [InlineData(data: ["Show.Name.NCED.mkv", "tv", "Show Name", 1])]
    [InlineData(data: ["My Show - Special 2.mkv", "tv", "My Show", 2])]
    [InlineData(data: ["My Show - Specials.mkv", "tv", "My Show", 1])]
    public void Specials_season_zero(string file, string lib, string title, int episode)
    {
        ParseContext context = Context(fileName: file, libraryType: lib);
        MovieFile result = Pipeline().Parse(context: context);
        result.IsSeries.Should().BeTrue();
        result.Title.Should().Be(expected: title);
        result.Season.Should().Be(expected: 0);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: context).Should().Be(expected: "specials");
    }

    [Fact]
    public void Specials_uses_folder_for_bare_anime_marker()
    {
        ParseContext context = Context(fileName: "OVA 2.mkv", folderTitle: "One Piece", libraryType: "anime");
        MovieFile result = Pipeline().Parse(context: context);
        result.Title.Should().Be(expected: "One Piece");
        result.Season.Should().Be(expected: 0);
        result.Episode.Should().Be(expected: 2);
        FirstMatchingAdapter(context: context).Should().Be(expected: "specials");
    }

    [Theory]
    // word markers never steal a real title, substrings never match
    [InlineData(data: "Special.mkv")]
    [InlineData(data: "Extras.mkv")]
    [InlineData(data: "Spectre.2015.1080p.mkv")]
    [InlineData(data: "Casanova.1981.mkv")]
    [InlineData(data: "Extraction.2020.1080p.mkv")]
    [InlineData(data: "Nova.2021.mkv")]
    public void Specials_does_not_over_classify(string file) =>
        new SpecialsAdapter().TryParse(context: Context(fileName: file)).Should().BeNull();

    [Fact]
    public void Specials_skipped_for_movie_libraries() =>
        new SpecialsAdapter()
            .TryParse(context: Context(fileName: "Show - OVA 1.mkv", libraryType: "movie"))
            .Should()
            .BeNull();

    [Fact]
    public void S00Exx_still_owned_by_season_episode()
    {
        ParseContext context = Context(fileName: "Bleach.S00E05.1080p.mkv");
        MovieFile result = Pipeline().Parse(context: context);
        result.Season.Should().Be(expected: 0);
        result.Episode.Should().Be(expected: 5);
        FirstMatchingAdapter(context: context).Should().Be(expected: "season-episode");
    }

    // ---------------------------------------------------------------------------
    // Real scene release names (scenerules.org spec; cf. pr0pz/scene-release-parser,
    // MIT). These lock title/season/episode extraction against names seen in the
    // wild, including year-in-title and split season.episode forms.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["Halo.2022.S01E06.POLISH.720p.WEB.H264-A4O.mkv", "Halo", 1, 6, "season-episode"])]
    [InlineData(data: ["Gilmore.Girls.S05E01.720p.WEB-DL.AAC2.0.H.264-tK.mkv", "Gilmore Girls", 5, 1, "season-episode"])]
    [InlineData(data: ["Direct.Talk.S09E09.Mizutani.Yoshihiro.Relief.1080p.HDTV.H264-DARKFLiX.mkv", "Direct Talk", 9, 9, "season-episode"])]
    [InlineData(data: ["Dark.Net.S01E06.DOC.SUBFRENCH.720p.WEBRip.x264-TiMELiNE.mkv", "Dark Net", 1, 6, "season-episode"])]
    [InlineData(data: ["New.Amsterdam.2018.S02E12.1080p.AMZN.Webrip.x265.10bit.EAC3.5.1.mkv", "New Amsterdam", 2, 12, "season-episode"])]
    [InlineData(data: ["Stranger.Things.S04E09.2160p.NF.WEB-DL.DDP5.1.x265-NTb.mkv", "Stranger Things", 4, 9, "season-episode"])]
    [InlineData(data: ["The.X-Files.2x14.Die.Hand.Die.Verletzt.DVDRip.XviD.MultiDub-VeLVeT.mkv", "The X-Files", 2, 14, "cross-format"])]
    [InlineData(data: ["New.Amsterdam.2018.2x12.1080p.WEBMux.x264-NovaRi.mkv", "New Amsterdam", 2, 12, "cross-format"])]
    [InlineData(data: ["24.Twenty.Four.S2.E07.German.DVDRiP.Line.Dubbed.SVCD-SOF.mkv", "24 Twenty Four", 2, 7, "episode-short-form"])]
    [InlineData(data: ["1883.S01E04.1080p.WEB.H264.mkv", "1883", 1, 4, "season-episode"])]
    [InlineData(data: ["1923.S01E02.720p.HDTV.x264.mkv", "1923", 1, 2, "season-episode"])]
    public void SceneCorpus_real_names(
        string file,
        string title,
        int season,
        int episode,
        string adapter
    )
    {
        ParseContext context = Context(fileName: file);
        MovieFile result = Pipeline().Parse(context: context);
        result.IsSeries.Should().BeTrue();
        result.Title.Should().Be(expected: title);
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
        FirstMatchingAdapter(context: context).Should().Be(expected: adapter);
    }

    [Theory]
    // a cross-format token that IS the title must not be read as SxExx
    [InlineData(data: "4x4.2019.1080p.BluRay.x264-GRP.mkv")]
    [InlineData(data: "4x4.mkv")]
    public void CrossFormat_ignores_leading_token(string file) =>
        new CrossFormatAdapter().TryParse(context: Context(fileName: file)).Should().BeNull();

    // ---------------------------------------------------------------------------
    // Hard real-world corpus (harvested from the MIT pr0pz/scene-release-parser
    // fixtures). These assert the *parse outcome* of long, messy, real scene names
    // — not the tokens a regex was built from — so they prove behaviour rather than
    // restating vocabulary. Several are adversarial (a title that opens with "4x4",
    // stray "9.00" numbers, a year before the SxxExx, an "x" inside the title).
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(data: ["24.S02E02.9.00.Uhr.bis.10.00.Uhr.German.DL.TV.Dubbed.DVDRip.SVCD.READ.NFO-c0nFuSed", 2, 2])]
    [InlineData(data: ["4x4.Ule.ja.Umber.Autoga.Colombias.S01E09.EE.1080p.WEB.h264-EMX", 1, 9])]
    [InlineData(data: ["72.Cutest.Animals.S01E0.German.DL.Doku.1080p.WEB.x264-BiGiNT", 1, 0])]
    [InlineData(data: ["Dark.Net.S01E06.DOC.SUBFRENCH.720p.WEBRip.x264-TiMELiNE", 1, 6])]
    [InlineData(data: ["Direct.Talk.S09E09.Mizutani.Yoshihiro.Relief.Beds.Made.of.Cardboard.1080p.HDTV.H264-DARKFLiX", 9, 9])]
    [InlineData(data: ["Gilmore.Girls.S05E01.720p.WEB-DL.AAC2.0.H.264-tK", 5, 1])]
    [InlineData(data: ["Halo.2022.S01E06.POLISH.720p.WEB.H264-A4O", 1, 6])]
    [InlineData(data: ["New.Amsterdam.2018.S02E12.14.Years.mkv", 2, 12])]
    [InlineData(data: ["New.Amsterdam.2018.2x12.14.Anni.2.Mesi.8.Giorni.ITA-ENG.1080p.WEBMux.x264-NovaRi", 2, 12])]
    public void Pipeline_ExtractsSeasonEpisode_FromHardRealNames(
        string file,
        int season,
        int episode
    )
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.Season.Should().Be(expected: season);
        result.Episode.Should().Be(expected: episode);
    }

    [Theory]
    [InlineData(data: ["Spy.x.Family.E04.Elterngespraech.an.der.Eliteschule.German.2022.ANiME.DL.BDRiP.x264-STARS", 4])]
    public void Pipeline_ExtractsEpisode_WhenNoSeason(string file, int episode)
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file));
        result.Episode.Should().Be(expected: episode);
    }

    [Theory]
    [InlineData(data: ["Gegen.den.Strom.2018.German.AC3D.DL.1080p.BluRay.x264-SAVASTANOS", "2018"])]
    [InlineData(data: ["Batman.v.Superman.Dawn.of.Justice.2016.IMAX.German.DL.TrueHD.Atmos.DUBBED.2160p.UHD.BluRay.x265-GSG9", "2016"])]
    [InlineData(data: ["Cloudy.With.A.Chance.Of.Meatballs.2009.NORDIC.DTS-HD.DTS.AC3.NORDICSUBS.1080p.BluRay.x264-TUSAHD", "2009"])]
    [InlineData(data: ["Pay.the.Ghost.2015.1080p.HULU.WEB-DL.DDP.5.1.H.264-PiRaTeS", "2015"])]
    [InlineData(data: ["Angel.Heart.1987.German.DTSMAD.5.1.DL.2160p.UHD.BluRay.HDR.DV.HEVC.Remux-HDSource", "1987"])]
    [InlineData(data: ["Burial.Ground.The.Nights.Of.Terror.1981.DUBBED.GRINDHOUSE.VERSION.1080P.BLURAY.X264-WATCHABLE", "1981"])]
    [InlineData(data: ["Intruders.Die.Aliens.Sind.Unter.Uns.1992.Uncut.German.AC3.DVDRiP.XviD", "1992"])]
    [InlineData(data: ["V.H.S.94.2021.BluRay.1080p.DTS-HD.MA.5.1.AVC-GROUPNAME", "2021"])]
    public void Pipeline_ClassifiesMovies_NotSeries(string file, string year)
    {
        MovieFile result = Pipeline().Parse(context: Context(fileName: file, folderTitle: "", libraryType: "movies"));
        result.IsSeries.Should().BeFalse();
        result.Year.Should().Be(expected: year);
    }

    // ---------------------------------------------------------------------------
    // Shared test-media corpus — the same real-world filename patterns the encoder
    // input→output tests synthesise. Parsing them here proves the corpus names are
    // classified correctly (movie vs series, season/episode) before any byte is
    // encoded, so a corpus entry and its parse expectation can never drift apart.
    // ---------------------------------------------------------------------------
    public static TheoryData<string> CorpusFiles()
    {
        TheoryData<string> data = [];
        foreach (MediaCorpusEntry entry in MediaCorpus.Entries)
            data.Add(p: entry.RelativePath);
        return data;
    }

    [Theory]
    [MemberData(memberName: nameof(CorpusFiles))]
    public void Corpus_filenames_parse_to_expected_classification(string relativePath)
    {
        MediaCorpusEntry entry = MediaCorpus.Entries.Single(predicate: e => e.RelativePath == relativePath);

        string fileName = Path.GetFileName(path: entry.RelativePath);
        string folderTitle =
            Path.GetFileName(path: Path.GetDirectoryName(path: entry.RelativePath) ?? string.Empty)
            ?? string.Empty;
        string libraryType = entry.ExpectedIsMovie ? "movies" : "tv";

        MovieFile result = Pipeline().Parse(context: Context(fileName: fileName, folderTitle: folderTitle, libraryType: libraryType));

        result.IsSeries.Should().Be(expected: !entry.ExpectedIsMovie);
        if (entry.ExpectedSeason is int season)
            result.Season.Should().Be(expected: season);
        if (entry.ExpectedEpisode is int episode)
            result.Episode.Should().Be(expected: episode);
    }
}
