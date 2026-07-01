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

using NoMercy.NmSystem.Extensions;

namespace NoMercy.Tests.NmSystem;

[Trait("Category", "Unit")]
public class StringExtensionsAdvancedTests
{
    [Theory]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("résumé", "resume")]
    public void RemoveDiacritics_StripsCombiningMarks(string input, string expected)
    {
        string result = input.RemoveDiacritics();
        result.Should().Be(expected);
    }

    [Fact]
    public void RemoveNonAlphaNumericCharacters_KeepsSpacesDashesAndDots()
    {
        string result = "Movie-2021.Title (Director's Cut)!".RemoveNonAlphaNumericCharacters();
        result.Should().Contain("Movie");
        result.Should().Contain("2021");
        result.Should().Contain("-");
        result.Should().NotContain("(");
        result.Should().NotContain("!");
    }

    [Theory]
    [InlineData("Movie 2020.mkv", 2020)]
    [InlineData("Title (2021) Extra.mkv", 2021)]
    [InlineData("2019 Release Date.mkv", 2019)]
    [InlineData("Film from 1999", 1999)]
    public void TryGetYear_ParsesFourDigitYear(string input, int expectedYear)
    {
        string? result = input.TryGetYear();
        result.Should().Be(expectedYear.ToString());
    }

    [Fact]
    public void TryGetYear_WithoutYear_ReturnsNull()
    {
        string? result = "No year here".TryGetYear();
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetYear_WithThreeDigitNumber_IgnoresIt()
    {
        string? result = "Movie 123 Title".TryGetYear();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Show.S01E05", "01", "05")]
    [InlineData("Series S02E10", "02", "10")]
    [InlineData("prefix S05E01 Start", "05", "01")]
    public void MatchSeasonEpisode_ExtractsFromText(
        string input,
        string expectedSeason,
        string expectedEpisode
    )
    {
        System.Text.RegularExpressions.Match match = StringExtensions
            .MatchSeasonEpisode()
            .Match(input);
        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be(expectedSeason);
        match.Groups[2].Value.Should().Be(expectedEpisode);
    }

    [Theory]
    [InlineData("Movie 2x05", "2", "05")]
    [InlineData("Show 1×10", "1", "10")]
    [InlineData("Series 3X08", "3", "08")]
    public void MatchCrossFormatEpisode_ExtractsSeasonEpisode(
        string input,
        string expectedSeason,
        string expectedEpisode
    )
    {
        System.Text.RegularExpressions.Match match = StringExtensions
            .MatchCrossFormatEpisode()
            .Match(input);
        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be(expectedSeason);
        match.Groups[2].Value.Should().Be(expectedEpisode);
    }

    [Theory]
    [InlineData("Movie.1080p.mkv", true)]
    [InlineData("Show.720p.mkv", true)]
    [InlineData("Film.4k.mkv", true)]
    [InlineData("Title.uhd.mkv", true)]
    [InlineData("No.resolution.mkv", false)]
    public void MatchResolutionTag_IdentifiesResolution(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchResolutionTag().IsMatch(input);
        matches.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("Movie.WEBRIP.mkv", true)]
    [InlineData("Show.BLURAY.mkv", true)]
    [InlineData("Film.DVDRip.mkv", true)]
    [InlineData("Title.HDTV.mkv", true)]
    [InlineData("No.source.mkv", false)]
    public void MatchSourceTag_IdentifiesSource(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchSourceTag().IsMatch(input);
        matches.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("Movie.H264.mkv", true)]
    [InlineData("Show.HEVC.mkv", true)]
    [InlineData("Film.XVID.mkv", true)]
    [InlineData("Title.x265.mkv", true)]
    [InlineData("No.codec.mkv", false)]
    public void MatchCodecTag_IdentifiesCodec(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchCodecTag().IsMatch(input);
        matches.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("Movie.AAC.mkv", true)]
    [InlineData("Show.DDP5.1.mkv", true)]
    [InlineData("Film.FLAC.mkv", true)]
    [InlineData("Title.AC3.mkv", true)]
    [InlineData("No.audio.mkv", false)]
    public void MatchAudioTag_IdentifiesAudio(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchAudioTag().IsMatch(input);
        matches.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("Movie.10bit.mkv", true)]
    [InlineData("Show.HDR10.mkv", true)]
    [InlineData("Film.DOVI.mkv", true)]
    [InlineData("Title.SDR.mkv", true)]
    [InlineData("Unknown.mkv", false)]
    public void MatchHdrTag_IdentifiesHdr(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchHdrTag().IsMatch(input);
        matches.Should().Be(shouldMatch);
    }

    [Theory]
    [InlineData("Movie.REPACK.mkv", true)]
    [InlineData("Show.MULTI.mkv", true)]
    [InlineData("Film.IMAX.mkv", true)]
    [InlineData("No.flag.mkv", false)]
    public void MatchFlagTag_IdentifiesFlag(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchFlagTag().IsMatch(input);
        matches.Should().Be(shouldMatch);
    }

    [Fact]
    public void TryGetReleaseTag_FindsFirstTag()
    {
        bool found = "Movie.1080p.WEBRIP.H264.mkv".TryGetReleaseTag(
            out string value,
            out StringExtensions.ReleaseTagCategory category
        );
        found.Should().BeTrue();
        value.Should().Be("1080p");
        category.Should().Be(StringExtensions.ReleaseTagCategory.Resolution);
    }

    [Fact]
    public void TryGetReleaseTag_EmptyString_ReturnsFalse()
    {
        bool found = "".TryGetReleaseTag(
            out string value,
            out StringExtensions.ReleaseTagCategory category
        );
        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetReleaseTag_NullString_ReturnsFalse()
    {
        bool found = ((string?)null)!.TryGetReleaseTag(
            out string value,
            out StringExtensions.ReleaseTagCategory category
        );
        found.Should().BeFalse();
    }

    [Theory]
    [InlineData("Movie.1080p.WEBRIP.H264.mkv")]
    [InlineData("Show.Season.2.HDTV.mkv")]
    [InlineData("Film 2021 720p")]
    public void CleanReleaseTitle_RemovesSceneTagsAndBeyond(string input)
    {
        string result = input.CleanReleaseTitle();
        result.Should().NotBeEmpty();
        result.Length.Should().BeLessThanOrEqualTo(input.Length);
    }

    [Fact]
    public void CleanReleaseTitle_WithoutTags_ReturnsAsIs()
    {
        string result = "Simple Movie Title".CleanReleaseTitle();
        result.Should().Be("Simple Movie Title");
    }

    [Theory]
    [InlineData("Show 2022", "Show")]
    [InlineData("New Amsterdam 2018", "New Amsterdam")]
    [InlineData("1883", "1883")]
    [InlineData("2021 Apocalypse", "2021 Apocalypse")]
    public void CleanSeriesTitle_RemovesTrailingYear(string input, string expected)
    {
        string result = input.CleanSeriesTitle();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/path/Season 02", 2)]
    [InlineData("Show/Series 5", 5)]
    [InlineData("X/S02", 2)]
    [InlineData("/media/saison 01", 1)]
    public void TryGetFolderSeason_ExtractsSeasonNumber(string path, int expected)
    {
        int? result = path.TryGetFolderSeason();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("NotASeason")]
    [InlineData("Show/Season A")]
    [InlineData("/path/movies")]
    public void TryGetFolderSeason_WithoutSeasonFolder_ReturnsNull(string path)
    {
        int? result = path.TryGetFolderSeason();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryGetFolderSeason_WithNullOrEmpty_ReturnsNull(string? path)
    {
        int? result = path.TryGetFolderSeason();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("HelloWorld")]
    [InlineData("ID")]
    [InlineData("lowercase")]
    public void SplitPascalCase_SplitsOnWordBoundaries(string input)
    {
        string result = input.SplitPascalCase();
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThanOrEqualTo(input.Length);
    }
}
