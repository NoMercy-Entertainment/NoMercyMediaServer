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

using System.IO;
using System.Linq;
using FluentAssertions;
using MovieFileLibrary;
using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Files.Parsing.Adapters;
using NoMercy.NmSystem.Extensions;
using Xunit;

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
            new SeasonEpisodeAdapter(),
            new MovieDetectorAdapter(),
        };

    private static IFilenameParserPipeline Pipeline() =>
        new FilenameParserPipeline(DefaultAdapters());

    private static ParseContext Context(string fileName, string folderTitle = "")
    {
        string cleaned = StringExtensions
            .RemoveBracketedString()
            .Replace(Path.GetFileNameWithoutExtension(fileName), string.Empty)
            .Trim();

        return new ParseContext
        {
            FileNameWithExtension = fileName,
            DirectoryName = null,
            Title = StringExtensions.RemoveBracketedString().Replace(fileName, string.Empty),
            CleanedFileName = cleaned,
            FolderTitle = folderTitle,
        };
    }

    /// <summary>The first adapter (in effective order) that returns a result.</summary>
    private static string? FirstMatchingAdapter(ParseContext context)
    {
        foreach (IFilenameParseAdapter adapter in DefaultAdapters().OrderBy(a => a.Order))
            if (adapter.TryParse(context) is not null)
                return adapter.Name;
        return null;
    }

    // ---------------------------------------------------------------------------
    // SxxExx anywhere (the bread-and-butter scene format)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("One.Piece.S01E1109.1080p.WEB-DL.mkv", 1, 1109)]
    [InlineData("The.Office.US.S03E12.720p.HDTV.x264-LOL.mkv", 3, 12)]
    [InlineData("Breaking.Bad.S05E14.1080p.BluRay.x265-GROUP.mkv", 5, 14)]
    [InlineData("Game of Thrones - S08E06 - The Iron Throne.mkv", 8, 6)]
    [InlineData("Mr_Robot_S02E05_720p.mkv", 2, 5)]
    [InlineData("Stranger.Things.S04E09.2160p.NF.WEB-DL.DDP5.1.x265-NTb.mkv", 4, 9)]
    [InlineData("Better.Call.Saul.S06E13.PROPER.1080p.mkv", 6, 13)]
    [InlineData("Rick and Morty S05E10 1080p.mkv", 5, 10)]
    [InlineData("Dark.S01E01.German.DL.1080p.BluRay.x264-GROUP.mkv", 1, 1)]
    [InlineData("The.Mandalorian.S02E08.2160p.WEB-DL.mkv", 2, 8)]
    [InlineData("Severance.S01E09.1080p.ATVP.WEBRip.mkv", 1, 9)]
    [InlineData("Show.Name.s1e3.mkv", 1, 3)]
    public void SeasonEpisode_anywhere_resolves(string file, int season, int episode)
    {
        MovieFile result = Pipeline().Parse(Context(file));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(season);
        result.Episode.Should().Be(episode);
        FirstMatchingAdapter(Context(file)).Should().Be("season-episode");
    }

    // ---------------------------------------------------------------------------
    // SxxExx at the very start (title comes from the folder)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("S01E05 - The Title.mkv", 1, 5)]
    [InlineData("S03E10.1080p.mkv", 3, 10)]
    [InlineData("s1e1.mkv", 1, 1)]
    [InlineData("S12E24-Finale.mkv", 12, 24)]
    public void EpisodePrefix_at_start_uses_folder_title(string file, int season, int episode)
    {
        const string folder = "The Show Name";
        MovieFile result = Pipeline().Parse(Context(file, folder));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(season);
        result.Episode.Should().Be(episode);
        result.Title.Should().Be(folder);
        FirstMatchingAdapter(Context(file, folder)).Should().Be("episode-prefix");
    }

    // ---------------------------------------------------------------------------
    // "Episode NN" word form (separator required before the word)
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("Blade - Episode 02 - title.mp4", 2)]
    [InlineData("Naruto Episode 130.mkv", 130)]
    [InlineData("Cowboy Bebop - Episode 5.mkv", 5)]
    [InlineData("Some.Show.Episode12.mkv", 12)]
    public void EpisodeWord_resolves(string file, int episode)
    {
        MovieFile result = Pipeline().Parse(Context(file));
        result.IsSeries.Should().BeTrue();
        result.Season.Should().Be(1);
        result.Episode.Should().Be(episode);
        FirstMatchingAdapter(Context(file)).Should().Be("episode-word");
    }

    // ---------------------------------------------------------------------------
    // Movies must NOT be under-classified as episodes by any series adapter.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("Inception.2010.1080p.BluRay.x264-GROUP.mkv")]
    [InlineData("The.Matrix.1999.REMASTERED.1080p.BluRay.x265.mkv")]
    [InlineData("Dune.Part.Two.2024.2160p.WEB-DL.DDP5.1.x265-FLUX.mkv")]
    [InlineData("Avengers.Endgame.2019.UHD.BluRay.2160p.mkv")]
    [InlineData("Blade Runner 2049 (2017).mkv")]
    [InlineData("Top.Gun.Maverick.2022.IMAX.1080p.mkv")]
    public void Movies_are_not_classified_as_episodes(string file)
    {
        ParseContext context = Context(file);
        new EpisodePrefixAdapter().TryParse(context).Should().BeNull();
        new EpisodeWordAdapter().TryParse(context).Should().BeNull();
        new SeasonEpisodeAdapter().TryParse(context).Should().BeNull();
        FirstMatchingAdapter(context).Should().Be("movie-detector");
    }

    // ---------------------------------------------------------------------------
    // Ordering / no under-classification: the FIRST adapter to match must be the
    // most specific correct one.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("One.Piece.S01E1109.mkv", "season-episode")]
    [InlineData("Blade - Episode 02.mkv", "episode-word")]
    [InlineData("Inception.2010.1080p.mkv", "movie-detector")]
    public void Pipeline_first_match_is_expected_adapter(string file, string expected) =>
        FirstMatchingAdapter(Context(file, "Folder Title")).Should().Be(expected);

    [Fact]
    public void EpisodePrefix_at_start_wins_over_season_episode_for_same_name()
    {
        // "S01E05..." matches the start-anchored prefix; the .SxxExx (separator
        // required) form deliberately does NOT match a leading token, so prefix owns it.
        ParseContext context = Context("S01E05.mkv", "My Show");
        new EpisodePrefixAdapter().TryParse(context).Should().NotBeNull();
        new SeasonEpisodeAdapter().TryParse(context).Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Raw regex contracts (positive + negative) so each pattern is pinned down.
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData("S01E01", true)]
    [InlineData("s1e1", true)]
    [InlineData("S01E1109", true)]
    [InlineData("S12E100", true)]
    [InlineData("Show.S01E01", false)]
    [InlineData("Movie.2010.1080p", false)]
    [InlineData("1x01", false)]
    public void MatchEpisodePrefix_contract(string input, bool expected) =>
        StringExtensions.MatchEpisodePrefix().IsMatch(input).Should().Be(expected);

    [Theory]
    [InlineData("Show.S01E01", true)]
    [InlineData("Show S01E01", true)]
    [InlineData("Show-S01E01", true)]
    [InlineData("Show_S02E05", true)]
    [InlineData("S01E01", false)]
    [InlineData("ShowS01E01", false)]
    [InlineData("Show.2010.1080p", false)]
    public void MatchSeasonEpisode_contract(string input, bool expected) =>
        StringExtensions.MatchSeasonEpisode().IsMatch(input).Should().Be(expected);

    [Theory]
    [InlineData(" Episode 5", true)]
    [InlineData("- Episode 02", true)]
    [InlineData(".Episode12", true)]
    [InlineData("Show Episode 130", true)]
    [InlineData("Episode 5", false)]
    [InlineData("Show.Episode.5", false)]
    public void MatchEpisodeWord_contract(string input, bool expected) =>
        StringExtensions.MatchEpisodeWord().IsMatch(input).Should().Be(expected);

    [Theory]
    [InlineData("Movie 2010", true)]
    [InlineData("Show 1999", true)]
    [InlineData("Film 2024", true)]
    [InlineData("Video 1080p", false)]
    [InlineData("Clip 2160p", false)]
    [InlineData("Thing 12345", false)]
    public void MatchYearRegex_contract(string input, bool expected) =>
        StringExtensions.MatchYearRegex().IsMatch(input).Should().Be(expected);

    // ---------------------------------------------------------------------------
    // Pipeline configurability: disable + reorder via FilenameParsingOptions.
    // ---------------------------------------------------------------------------
    [Fact]
    public void Disabling_an_adapter_removes_it_from_the_pipeline()
    {
        FilenameParsingOptions options = new() { Disabled = { "season-episode" } };
        FilenameParserPipeline pipeline = new(DefaultAdapters(), options);
        pipeline.Order.Should().NotContain("season-episode");
        pipeline.Order.Should().Contain("movie-detector");
    }

    [Fact]
    public void Order_override_runs_named_adapter_first()
    {
        FilenameParsingOptions options = new() { Order = { "movie-detector" } };
        FilenameParserPipeline pipeline = new(DefaultAdapters(), options);
        pipeline.Order.First().Should().Be("movie-detector");
    }
}
