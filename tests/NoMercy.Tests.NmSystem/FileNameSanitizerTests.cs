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

[Trait(name: "Category", value: "Unit")]
public class FileNameSanitizerTests
{
    [Fact]
    public void CleanFileName_WithEmptyString_ReturnsEmpty()
    {
        string result = "".CleanFileName();
        result.Should().Be(expected: "");
    }

    [Fact]
    public void CleanFileName_WithNull_ReturnsEmpty()
    {
        string result = ((string?)null).CleanFileName();
        result.Should().Be(expected: "");
    }

    [Fact]
    public void CleanFileName_WithNormalName_ReturnsUnchanged()
    {
        string result = "MovieTitle.mkv".CleanFileName();
        result.Should().Be(expected: "MovieTitle.mkv");
    }

    [Fact]
    public void CleanFileName_WithInvalidChars_ReplacesWithDots()
    {
        string result = "Movie|Title?.mkv".CleanFileName();
        result.Should().NotContain(unexpected: "|");
        result.Should().NotContain(unexpected: "?");
    }

    [Fact]
    public void CleanFileName_WithWhitespace_ReplacesWithDots()
    {
        string result = "Movie Title.mkv".CleanFileName();
        result.Should().Contain(expected: ".");
    }

    [Fact]
    public void CleanFileName_WithAmpersand_ReplacesWithAnd()
    {
        string result = "Movie&Title.mkv".CleanFileName();
        result.Should().Contain(expected: "and");
    }

    [Fact]
    public void CleanFileName_WithDegreesSign_ReplacesWithText()
    {
        string result = "90°.mkv".CleanFileName();
        result.Should().Contain(expected: "Degrees");
    }

    [Fact]
    public void CleanFileName_WithDashes_ReplacesManyWithOne()
    {
        string result = "Movie—Title–Test–Case.mkv".CleanFileName();
        result.Should().Contain(expected: "-");
    }

    [Fact]
    public void CleanFileName_WithMultipleDots_CollapsesToOne()
    {
        string result = "Movie...Title...Name.mkv".CleanFileName();
        result.Should().NotContain(unexpected: "...");
    }

    [Fact]
    public void CleanFileName_WithLeadingTrailingDots_RemovesThem()
    {
        string result = ".MovieTitle.".CleanFileName();
        result.Should().NotStartWith(unexpected: ".");
        result.Should().NotEndWith(unexpected: ".");
    }

    [Fact]
    public void DirectorySafeName_WithNull_ReturnsEmpty()
    {
        string result = ((string?)null).DirectorySafeName();
        result.Should().Be(expected: "");
    }

    [Fact]
    public void DirectorySafeName_WithNormalName_ReturnsName()
    {
        string result = "MyFolder".DirectorySafeName();
        result.Should().Be(expected: "MyFolder");
    }

    [Fact]
    public void DirectorySafeName_WithInvalidChars_ReplacesWithSpaces()
    {
        string result = "Folder/Name|Other".DirectorySafeName();
        result.Should().NotContain(unexpected: "/");
        result.Should().NotContain(unexpected: "|");
    }

    [Fact]
    public void MusicBrainzSafeName_WithNull_ReturnsEmpty()
    {
        string result = ((string?)null).MusicBrainzSafeName();
        result.Should().Be(expected: "");
    }

    [Fact]
    public void MusicBrainzSafeName_WithNormalName_ReturnsName()
    {
        string result = "ArtistName".MusicBrainzSafeName();
        result.Should().Be(expected: "ArtistName");
    }

    [Fact]
    public void MusicBrainzSafeName_WithInvalidChars_ReplacesWithUnderscores()
    {
        string result = "Artist/Name|Other".MusicBrainzSafeName();
        result.Should().NotContain(unexpected: "/");
        result.Should().NotContain(unexpected: "|");
    }

    [Fact]
    public void SanitizeFileName_WithSmartQuotes_ConvertsToAscii()
    {
        string result = "movie’s tale.mkv".SanitizeFileName();
        result.Should().Contain(expected: "'");
    }

    [Fact]
    public void SanitizeFileName_WithLeftDoubleQuote_ConvertsToAscii()
    {
        string result = "“movie”.mkv".SanitizeFileName();
        result.Should().Contain(expected: "\"");
    }

    [Fact]
    public void SanitizeFileName_WithEnDash_ConvertsToHyphen()
    {
        string result = "movie–name.mkv".SanitizeFileName();
        result.Should().Contain(expected: "-");
    }

    [Theory]
    [InlineData(data: ["simple", "simple"])]
    [InlineData(data: ["with spaces", "withspaces"])]
    [InlineData(data: ["with-dashes", "withdashes"])]
    [InlineData(data: ["CamelCase", "camelcase"])]
    public void NormalizeForComparison_StripsNonAlphanumeric(string input, string expected)
    {
        string result = input.NormalizeForComparison();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void NormalizeForComparison_ConvertAmpersand()
    {
        string result = "A&B".NormalizeForComparison();
        result.Should().Be(expected: "aandb");
    }

    [Fact]
    public void NormalizeForComparison_IsCaseInsensitive()
    {
        string result = "HELLO".NormalizeForComparison();
        result.Should().Be(expected: "hello");
    }

    [Fact]
    public void Shorten_WithinLimit_ReturnsUnchanged()
    {
        // Short titles keep their exact existing path — no relocation of content.
        "Breaking Bad".Shorten().Should().Be(expected: "Breaking Bad");
    }

    [Fact]
    public void Shorten_NullOrEmpty_ReturnsEmpty()
    {
        ((string?)null).Shorten().Should().Be(expected: "");
        "".Shorten().Should().Be(expected: "");
    }

    [Fact]
    public void Shorten_LongInput_IsBoundedToMaxLength()
    {
        string result = new string(c: 'a', count: 200).Shorten();
        result.Length.Should().BeLessThanOrEqualTo(expected: FileNameSanitizer.MaxTitleComponentLength);
    }

    [Fact]
    public void Shorten_IsDeterministic()
    {
        const string longTitle =
            "My.Gift.Lvl.9999.Unlimited.Gacha.Backstabbed.in.a.Backwater.Dungeon.Im.Out.for.Revenge";
        longTitle.Shorten().Should().Be(expected: longTitle.Shorten());
    }

    [Fact]
    public void Shorten_LongTitle_IsStillReadable_AndCarriesNoDigest()
    {
        // The name a viewer reads is the start of the real title and nothing else.
        // Two titles sharing a 50-char prefix do now produce the same component;
        // what separates them lives in the name this is embedded in — an episode's
        // SxxEyy marker, a movie's release year — which is where it belonged.
        string cleaned =
            "OO Magic Episode 3: The Magic of Waking Up at a Certain Time in the Morning".CleanFileName();

        string result = cleaned.Shorten();

        result.Should().Be(expected: "OO.Magic.Episode.3.The.Magic.of.Waking.Up.at.a");
        result.Should().NotMatchRegex(regularExpression: "[0-9a-f]{8}$", because: "a digest is not part of a title");
    }

    [Fact]
    public void Shorten_UsesTheWholeBudgetForTheTitle()
    {
        // The digest used to eat nine of the fifty characters, so a title was cut
        // far shorter than the limit actually required.
        string cleaned = new string(c: 'a', count: 60);

        cleaned
            .Shorten()
            .Length.Should()
            .Be(expected: FileNameSanitizer.MaxTitleComponentLength, because: "every character of the cap is title");
    }

    [Fact]
    public void Shorten_RealAnimeTitle_FitsAndKeepsRecognizablePrefix()
    {
        string cleaned =
            "My Gift LVL 9999 Unlimited Gacha Backstabbed in a Backwater Dungeon Im Out for Revenge".CleanFileName();

        string result = cleaned.Shorten();

        result.Length.Should().BeLessThanOrEqualTo(expected: FileNameSanitizer.MaxTitleComponentLength);
        result.Should().StartWith(expected: "My.Gift");
    }
}
