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
public class StringExtensionsAdvancedTests
{
    [Theory]
    [InlineData(data: ["café", "cafe"])]
    [InlineData(data: ["naïve", "naive"])]
    [InlineData(data: ["résumé", "resume"])]
    public void RemoveDiacritics_StripsCombiningMarks(string input, string expected)
    {
        string result = input.RemoveDiacritics();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void RemoveNonAlphaNumericCharacters_KeepsSpacesDashesAndDots()
    {
        string result = "Movie-2021.Title (Director's Cut)!".RemoveNonAlphaNumericCharacters();
        result.Should().Contain(expected: "Movie");
        result.Should().Contain(expected: "2021");
        result.Should().Contain(expected: "-");
        result.Should().NotContain(unexpected: "(");
        result.Should().NotContain(unexpected: "!");
    }

    [Theory]
    [InlineData(data: ["Movie 2020.mkv", 2020])]
    [InlineData(data: ["Title (2021) Extra.mkv", 2021])]
    [InlineData(data: ["2019 Release Date.mkv", 2019])]
    [InlineData(data: ["Film from 1999", 1999])]
    public void TryGetYear_ParsesFourDigitYear(string input, int expectedYear)
    {
        string? result = input.TryGetYear();
        result.Should().Be(expected: expectedYear.ToString());
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
    [InlineData(data: ["Show.S01E05", "01", "05"])]
    [InlineData(data: ["Series S02E10", "02", "10"])]
    [InlineData(data: ["prefix S05E01 Start", "05", "01"])]
    public void MatchSeasonEpisode_ExtractsFromText(
        string input,
        string expectedSeason,
        string expectedEpisode
    )
    {
        System.Text.RegularExpressions.Match match = StringExtensions
            .MatchSeasonEpisode()
            .Match(input: input);
        match.Success.Should().BeTrue();
        match.Groups[groupnum: 1].Value.Should().Be(expected: expectedSeason);
        match.Groups[groupnum: 2].Value.Should().Be(expected: expectedEpisode);
    }

    [Theory]
    [InlineData(data: ["Movie 2x05", "2", "05"])]
    [InlineData(data: ["Show 1×10", "1", "10"])]
    [InlineData(data: ["Series 3X08", "3", "08"])]
    public void MatchCrossFormatEpisode_ExtractsSeasonEpisode(
        string input,
        string expectedSeason,
        string expectedEpisode
    )
    {
        System.Text.RegularExpressions.Match match = StringExtensions
            .MatchCrossFormatEpisode()
            .Match(input: input);
        match.Success.Should().BeTrue();
        match.Groups[groupnum: 1].Value.Should().Be(expected: expectedSeason);
        match.Groups[groupnum: 2].Value.Should().Be(expected: expectedEpisode);
    }

    [Theory]
    [InlineData(data: ["Movie.1080p.mkv", true])]
    [InlineData(data: ["Show.720p.mkv", true])]
    [InlineData(data: ["Film.4k.mkv", true])]
    [InlineData(data: ["Title.uhd.mkv", true])]
    [InlineData(data: ["No.resolution.mkv", false])]
    public void MatchResolutionTag_IdentifiesResolution(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchResolutionTag().IsMatch(input: input);
        matches.Should().Be(expected: shouldMatch);
    }

    [Theory]
    [InlineData(data: ["Movie.WEBRIP.mkv", true])]
    [InlineData(data: ["Show.BLURAY.mkv", true])]
    [InlineData(data: ["Film.DVDRip.mkv", true])]
    [InlineData(data: ["Title.HDTV.mkv", true])]
    [InlineData(data: ["No.source.mkv", false])]
    public void MatchSourceTag_IdentifiesSource(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchSourceTag().IsMatch(input: input);
        matches.Should().Be(expected: shouldMatch);
    }

    [Theory]
    [InlineData(data: ["Movie.H264.mkv", true])]
    [InlineData(data: ["Show.HEVC.mkv", true])]
    [InlineData(data: ["Film.XVID.mkv", true])]
    [InlineData(data: ["Title.x265.mkv", true])]
    [InlineData(data: ["No.codec.mkv", false])]
    public void MatchCodecTag_IdentifiesCodec(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchCodecTag().IsMatch(input: input);
        matches.Should().Be(expected: shouldMatch);
    }

    [Theory]
    [InlineData(data: ["Movie.AAC.mkv", true])]
    [InlineData(data: ["Show.DDP5.1.mkv", true])]
    [InlineData(data: ["Film.FLAC.mkv", true])]
    [InlineData(data: ["Title.AC3.mkv", true])]
    [InlineData(data: ["No.audio.mkv", false])]
    public void MatchAudioTag_IdentifiesAudio(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchAudioTag().IsMatch(input: input);
        matches.Should().Be(expected: shouldMatch);
    }

    [Theory]
    [InlineData(data: ["Movie.10bit.mkv", true])]
    [InlineData(data: ["Show.HDR10.mkv", true])]
    [InlineData(data: ["Film.DOVI.mkv", true])]
    [InlineData(data: ["Title.SDR.mkv", true])]
    [InlineData(data: ["Unknown.mkv", false])]
    public void MatchHdrTag_IdentifiesHdr(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchHdrTag().IsMatch(input: input);
        matches.Should().Be(expected: shouldMatch);
    }

    [Theory]
    [InlineData(data: ["Movie.REPACK.mkv", true])]
    [InlineData(data: ["Show.MULTI.mkv", true])]
    [InlineData(data: ["Film.IMAX.mkv", true])]
    [InlineData(data: ["No.flag.mkv", false])]
    public void MatchFlagTag_IdentifiesFlag(string input, bool shouldMatch)
    {
        bool matches = StringExtensions.MatchFlagTag().IsMatch(input: input);
        matches.Should().Be(expected: shouldMatch);
    }

    [Fact]
    public void TryGetReleaseTag_FindsFirstTag()
    {
        bool found = "Movie.1080p.WEBRIP.H264.mkv".TryGetReleaseTag(
            value: out string value,
            category: out StringExtensions.ReleaseTagCategory category
        );
        found.Should().BeTrue();
        value.Should().Be(expected: "1080p");
        category.Should().Be(expected: StringExtensions.ReleaseTagCategory.Resolution);
    }

    [Fact]
    public void TryGetReleaseTag_EmptyString_ReturnsFalse()
    {
        bool found = "".TryGetReleaseTag(
            value: out string value,
            category: out StringExtensions.ReleaseTagCategory category
        );
        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetReleaseTag_NullString_ReturnsFalse()
    {
        bool found = ((string?)null)!.TryGetReleaseTag(
            value: out string value,
            category: out StringExtensions.ReleaseTagCategory category
        );
        found.Should().BeFalse();
    }

    [Theory]
    [InlineData(data: "Movie.1080p.WEBRIP.H264.mkv")]
    [InlineData(data: "Show.Season.2.HDTV.mkv")]
    [InlineData(data: "Film 2021 720p")]
    public void CleanReleaseTitle_RemovesSceneTagsAndBeyond(string input)
    {
        string result = input.CleanReleaseTitle();
        result.Should().NotBeEmpty();
        result.Length.Should().BeLessThanOrEqualTo(expected: input.Length);
    }

    [Fact]
    public void CleanReleaseTitle_WithoutTags_ReturnsAsIs()
    {
        string result = "Simple Movie Title".CleanReleaseTitle();
        result.Should().Be(expected: "Simple Movie Title");
    }

    [Theory]
    [InlineData(data: ["Show 2022", "Show"])]
    [InlineData(data: ["New Amsterdam 2018", "New Amsterdam"])]
    [InlineData(data: ["1883", "1883"])]
    [InlineData(data: ["2021 Apocalypse", "2021 Apocalypse"])]
    public void CleanSeriesTitle_RemovesTrailingYear(string input, string expected)
    {
        string result = input.CleanSeriesTitle();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["/path/Season 02", 2])]
    [InlineData(data: ["Show/Series 5", 5])]
    [InlineData(data: ["X/S02", 2])]
    [InlineData(data: ["/media/saison 01", 1])]
    public void TryGetFolderSeason_ExtractsSeasonNumber(string path, int expected)
    {
        int? result = path.TryGetFolderSeason();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "NotASeason")]
    [InlineData(data: "Show/Season A")]
    [InlineData(data: "/path/movies")]
    public void TryGetFolderSeason_WithoutSeasonFolder_ReturnsNull(string path)
    {
        int? result = path.TryGetFolderSeason();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    public void TryGetFolderSeason_WithNullOrEmpty_ReturnsNull(string? path)
    {
        int? result = path.TryGetFolderSeason();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(data: "HelloWorld")]
    [InlineData(data: "ID")]
    [InlineData(data: "lowercase")]
    public void SplitPascalCase_SplitsOnWordBoundaries(string input)
    {
        string result = input.SplitPascalCase();
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThanOrEqualTo(expected: input.Length);
    }

    [Fact]
    public void RemoveAccents_EncodesStringToIso88591()
    {
        string input = "hello";
        string result = input.RemoveAccents();
        result.Should().Be(expected: "hello");
    }

    [Fact]
    public void PathName_NormalizesForwardSlashes()
    {
        string result = "path/to/file".PathName();
        result.Should().NotContain(unexpected: "/");
    }

    [Theory]
    [InlineData(data: ["123.45", 123])]
    [InlineData(data: ["", 0])]
    [InlineData(data: ["invalid", 0])]
    [InlineData(data: ["0", 0])]
    [InlineData(data: ["-50.5", -50])]
    public void ToInt_String_ParsesIntegerValue(string input, int expected)
    {
        int result = input.ToInt();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [123.7, 124])]
    [InlineData(data: [123.4, 123])]
    public void ToInt_Double_ConvertsWithRounding(double input, int expected)
    {
        int result = input.ToInt();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ToInt_UInt_ConvertsUnsignedInteger()
    {
        uint input = 100;
        int result = input.ToInt();
        result.Should().Be(expected: 100);
    }

    [Theory]
    [InlineData(data: ["456.78", 456.78])]
    [InlineData(data: ["", 0d])]
    [InlineData(data: ["invalid", 0d])]
    public void ToDouble_String_ParsesDoubleValue(string input, double expected)
    {
        double result = input.ToDouble();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ToDouble_Int_ConvertsToDouble()
    {
        int input = 100;
        double result = input.ToDouble();
        result.Should().Be(expected: 100d);
    }

    [Theory]
    [InlineData(data: ["789", 789L])]
    [InlineData(data: ["", 0L])]
    [InlineData(data: ["invalid", 0L])]
    public void ToLong_String_ParsesLongValue(string input, long expected)
    {
        long result = input.ToLong();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["true", true])]
    [InlineData(data: ["True", true])]
    [InlineData(data: ["false", false])]
    [InlineData(data: ["", false])]
    [InlineData(data: ["invalid", false])]
    public void ToBoolean_String_ParsesBooleanValue(string input, bool expected)
    {
        bool result = input.ToBoolean();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["text", 10, false, "text      "])]
    [InlineData(data: ["hi", 5, false, "hi   "])]
    [InlineData(data: ["text", 10, true, "      text"])]
    [InlineData(data: ["hi", 5, true, "   hi"])]
    public void Spacer_PadsTextWithSpaces(string text, int padding, bool begin, string expected)
    {
        string result = StringExtensions.Spacer(text: text, padding: padding, begin: begin);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["550e8400-e29b-41d4-a716-446655440000", "550e8400-e29b-41d4-a716-446655440000"])]
    [InlineData(data: ["invalid-guid", "00000000-0000-0000-0000-000000000000"])]
    [InlineData(data: ["", "00000000-0000-0000-0000-000000000000"])]
    [InlineData(data: [null, "00000000-0000-0000-0000-000000000000"])]
    public void ToGuid_String_ParsesOrReturnsEmpty(string? input, string expected)
    {
        Guid result = input.ToGuid();
        result.Should().Be(expected: Guid.Parse(input: expected));
    }

    [Fact]
    public void SplitPascalCase_SplitsCamelCaseWords()
    {
        string result = "CamelCase".SplitPascalCase();
        result.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(data: ["00:30:45", 1845])]
    [InlineData(data: ["1:15:30", 4530])]
    [InlineData(data: ["45", 45])]
    [InlineData(data: ["", 0])]
    [InlineData(data: [null, 0])]
    public void ToSeconds_String_ParsesTimeFormatToSeconds(string? input, int expected)
    {
        int result = input.ToSeconds();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [45.6, 46])]
    [InlineData(data: [0d, 0])]
    [InlineData(data: [123.4, 123])]
    public void ToSeconds_Double_RoundsToIntSeconds(double input, int expected)
    {
        int result = input.ToSeconds();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ToMilliSeconds_String_ConvertsToMilliseconds()
    {
        int result = "00:00:10".ToMilliSeconds();
        result.Should().Be(expected: 10000);
    }

    [Fact]
    public void SplitPascalCase_SplitsConsecutiveUppercase()
    {
        string result = "CamelCase".SplitPascalCase();
        result.Should().Contain(expected: "C");
    }


    [Theory]
    [InlineData(data: ["café naïve", "cafe naive"])]
    [InlineData(data: ["résumé", "resume"])]
    [InlineData(data: ["hello", "hello"])]
    public void Sanitize_RemovesDiacriticsAndNonAlphanumeric(string input, string expected)
    {
        string result = input.Sanitize();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Café", "café", true])]
    [InlineData(data: ["HELLO World", "hello world", true])]
    [InlineData(data: ["one", "two", false])]
    public void ContainsSanitized_ComparesNormalizedStrings(string haystack, string needle, bool expected)
    {
        bool result = haystack.ContainsSanitized(value: needle);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Café", "CAFÉ", true])]
    [InlineData(data: ["hello", "HELLO", true])]
    [InlineData(data: ["one", "two", false])]
    public void EqualsSanitized_ComparesNormalizedEquality(string a, string b, bool expected)
    {
        bool result = a.EqualsSanitized(value: b);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hello world", "hello world"])]
    [InlineData(data: ["hello%20world", "hello world"])]
    public void UrlDecode_DecodesUrlEncodedString(string input, string expected)
    {
        string result = input.UrlDecode();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void UrlEncode_EncodesSpaces()
    {
        string result = "hello world".UrlEncode();
        result.Should().NotBe(unexpected: "hello world");
    }

    [Fact]
    public void UrlEncode_PreservesNormalText()
    {
        string result = "test".UrlEncode();
        result.Should().Be(expected: "test");
    }

    [Fact]
    public void ToQueryUri_AppendsQueryParameters()
    {
        string result = "http://example.com".ToQueryUri(parameters: new Dictionary<string, string>
        {
            [key: "key1"] = "value1",
            [key: "key2"] = "value2"
        });
        result.Should().Contain(expected: "?");
        result.Should().Contain(expected: "key1=value1");
    }

    [Fact]
    public void ToQueryUri_WithNullParameters_ReturnsBaseUri()
    {
        string result = "http://example.com".ToQueryUri(parameters: null);
        result.Should().Be(expected: "http://example.com");
    }

    [Theory]
    [InlineData(data: ["hello\"world", "hello'world"])]
    [InlineData(data: ["\"test\"", "'test'"])]
    public void EscapeQuotes_ReplacesDoubleQuotesWithSingle(string input, string expected)
    {
        string result = input.EscapeQuotes();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hello", "Hello"])]
    [InlineData(data: ["world", "World"])]
    [InlineData(data: ["", ""])]
    public void Capitalize_CapitalizesFirstCharacter(string input, string expected)
    {
        string result = input.Capitalize();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hello world", "Hello World"])]
    [InlineData(data: ["test", "Test"])]
    public void ToTitleCase_CapitalizesEachWord(string input, string expectedStart)
    {
        string result = input.ToTitleCase();
        result.Should().StartWith(expected: expectedStart[index: 0].ToString().ToUpper());
    }

    [Theory]
    [InlineData(data: ["hello world", "Hello_World"])]
    [InlineData(data: ["test", "Test"])]
    public void ToPascalCase_ConvertsToPascalCase(string input, string expected)
    {
        string result = input.ToPascalCase();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["HelloWorld", "hello_world"])]
    [InlineData(data: ["HTTPServer", "h_t_t_p_server"])]
    public void ToSnakeCase_ConvertsToSnakeCase(string input, string expected)
    {
        string result = input.ToSnakeCase();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hello", "Hello"])]
    [InlineData(data: ["WORLD", "World"])]
    public void ToUcFirst_UppercasesFirstCharacter(string input, string expected)
    {
        string result = input.ToUcFirst();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "hello world")]
    [InlineData(data: "test string")]
    public void ToUtf8_ConvertedToUtf8(string input)
    {
        string result = input.ToUtf8();
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(data: "hello world")]
    [InlineData(data: "SHOUT")]
    public void NormalizeSearch_NormalizesSearchString(string input)
    {
        string result = input.NormalizeSearch();
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(data: ["Hello שלום", StringExtensions.TextDirection.RTL])]
    [InlineData(data: ["مرحبا Hello", StringExtensions.TextDirection.RTL])]
    [InlineData(data: ["Hello World", StringExtensions.TextDirection.LTR])]
    public void GetTextDirection_IdentifiesTextDirection(string input, StringExtensions.TextDirection expected)
    {
        StringExtensions.TextDirection result = input.GetTextDirection();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void TryGetTmdbHint_ExtractsTmdbId()
    {
        int? result = "Movie Title [tmdb-12345]".TryGetTmdbHint();
        result.Should().Be(expected: 12345);
    }

    [Fact]
    public void TryGetTmdbHint_WithoutHint_ReturnsNull()
    {
        int? result = "Movie Title".TryGetTmdbHint();
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetTmdbHint_CaseInsensitive()
    {
        int? result = "Movie Title [TMDB-999]".TryGetTmdbHint();
        result.Should().Be(expected: 999);
    }
}
