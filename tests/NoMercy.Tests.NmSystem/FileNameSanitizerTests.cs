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
public class FileNameSanitizerTests
{
    [Fact]
    public void CleanFileName_WithEmptyString_ReturnsEmpty()
    {
        string result = "".CleanFileName();
        result.Should().Be("");
    }

    [Fact]
    public void CleanFileName_WithNull_ReturnsEmpty()
    {
        string result = ((string?)null).CleanFileName();
        result.Should().Be("");
    }

    [Fact]
    public void CleanFileName_WithNormalName_ReturnsUnchanged()
    {
        string result = "MovieTitle.mkv".CleanFileName();
        result.Should().Be("MovieTitle.mkv");
    }

    [Fact]
    public void CleanFileName_WithInvalidChars_ReplacesWithDots()
    {
        string result = "Movie|Title?.mkv".CleanFileName();
        result.Should().NotContain("|");
        result.Should().NotContain("?");
    }

    [Fact]
    public void CleanFileName_WithWhitespace_ReplacesWithDots()
    {
        string result = "Movie Title.mkv".CleanFileName();
        result.Should().Contain(".");
    }

    [Fact]
    public void CleanFileName_WithAmpersand_ReplacesWithAnd()
    {
        string result = "Movie&Title.mkv".CleanFileName();
        result.Should().Contain("and");
    }

    [Fact]
    public void CleanFileName_WithDegreesSign_ReplacesWithText()
    {
        string result = "90°.mkv".CleanFileName();
        result.Should().Contain("Degrees");
    }

    [Fact]
    public void CleanFileName_WithDashes_ReplacesManyWithOne()
    {
        string result = "Movie—Title–Test–Case.mkv".CleanFileName();
        result.Should().Contain("-");
    }

    [Fact]
    public void CleanFileName_WithMultipleDots_CollapsesToOne()
    {
        string result = "Movie...Title...Name.mkv".CleanFileName();
        result.Should().NotContain("...");
    }

    [Fact]
    public void CleanFileName_WithLeadingTrailingDots_RemovesThem()
    {
        string result = ".MovieTitle.".CleanFileName();
        result.Should().NotStartWith(".");
        result.Should().NotEndWith(".");
    }

    [Fact]
    public void DirectorySafeName_WithNull_ReturnsEmpty()
    {
        string result = ((string?)null).DirectorySafeName();
        result.Should().Be("");
    }

    [Fact]
    public void DirectorySafeName_WithNormalName_ReturnsName()
    {
        string result = "MyFolder".DirectorySafeName();
        result.Should().Be("MyFolder");
    }

    [Fact]
    public void DirectorySafeName_WithInvalidChars_ReplacesWithSpaces()
    {
        string result = "Folder/Name|Other".DirectorySafeName();
        result.Should().NotContain("/");
        result.Should().NotContain("|");
    }

    [Fact]
    public void MusicBrainzSafeName_WithNull_ReturnsEmpty()
    {
        string result = ((string?)null).MusicBrainzSafeName();
        result.Should().Be("");
    }

    [Fact]
    public void MusicBrainzSafeName_WithNormalName_ReturnsName()
    {
        string result = "ArtistName".MusicBrainzSafeName();
        result.Should().Be("ArtistName");
    }

    [Fact]
    public void MusicBrainzSafeName_WithInvalidChars_ReplacesWithUnderscores()
    {
        string result = "Artist/Name|Other".MusicBrainzSafeName();
        result.Should().NotContain("/");
        result.Should().NotContain("|");
    }

    [Fact]
    public void SanitizeFileName_WithSmartQuotes_ConvertsToAscii()
    {
        string result = "movie’s tale.mkv".SanitizeFileName();
        result.Should().Contain("'");
    }

    [Fact]
    public void SanitizeFileName_WithLeftDoubleQuote_ConvertsToAscii()
    {
        string result = "“movie”.mkv".SanitizeFileName();
        result.Should().Contain("\"");
    }

    [Fact]
    public void SanitizeFileName_WithEnDash_ConvertsToHyphen()
    {
        string result = "movie–name.mkv".SanitizeFileName();
        result.Should().Contain("-");
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("with spaces", "withspaces")]
    [InlineData("with-dashes", "withdashes")]
    [InlineData("CamelCase", "camelcase")]
    public void NormalizeForComparison_StripsNonAlphanumeric(string input, string expected)
    {
        string result = input.NormalizeForComparison();
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeForComparison_ConvertAmpersand()
    {
        string result = "A&B".NormalizeForComparison();
        result.Should().Be("aandb");
    }

    [Fact]
    public void NormalizeForComparison_IsCaseInsensitive()
    {
        string result = "HELLO".NormalizeForComparison();
        result.Should().Be("hello");
    }
}
