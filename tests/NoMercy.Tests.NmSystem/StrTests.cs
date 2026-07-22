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

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins the pure string helpers in <see cref="Str"/> — fuzzy matching, filename
/// metadata extraction, sanitization, case conversion and numeric parsing — that
/// the scanner and metadata pipeline lean on. No DB, network or filesystem.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class StrTests
{
    [Theory]
    [InlineData(data: ["hello", "hello", 100.0])]
    [InlineData(data: ["abc", "abd", 200.0 / 3.0])]
    [InlineData(data: ["", "anything", 0.0])]
    [InlineData(data: ["anything", "", 0.0])]
    public void MatchPercentage_ScoresSimilarity(string a, string b, double expected)
    {
        FuzzyMatcher.MatchPercentage(strA: a, strB: b).Should().BeApproximately(expectedValue: expected, precision: 0.001);
    }

    [Fact]
    public void MatchPercentage_IsCaseInsensitive()
    {
        FuzzyMatcher.MatchPercentage(strA: "Hello", strB: "hello").Should().Be(expected: 100.0);
    }

    [Theory]
    [InlineData(data: ["Movie.2009.1080p.mkv", "2009"])]
    [InlineData(data: ["Some Film (2021)", "2021"])]
    [InlineData(data: ["Western 1899", "1899"])]
    [InlineData(data: ["1080p", null])]
    [InlineData(data: ["no year here", null])]
    public void TryGetYear_ExtractsFourDigitYear(string input, string? expected)
    {
        input.TryGetYear().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Movie [tmdb-1234] 1080p.mkv", 1234])]
    [InlineData(data: ["Show [TMDB-99].mkv", 99])]
    [InlineData(data: ["No hint here.mkv", null])]
    public void TryGetTmdbHint_ExtractsId(string input, int? expected)
    {
        input.TryGetTmdbHint().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["A & B!", "aandb"])]
    [InlineData(data: ["The Matrix", "thematrix"])]
    [InlineData(data: ["café", "caf"])] // non-ASCII stripped, not transliterated
    public void NormalizeForComparison_StripsToLowerAlphaNumeric(string input, string expected)
    {
        input.NormalizeForComparison().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["1:02:03", 3723])]
    [InlineData(data: ["02:03", 123])]
    [InlineData(data: ["00:00:00", 0])]
    [InlineData(data: ["1:00:00.500", 3600])] // fractional part dropped
    [InlineData(data: ["", 0])]
    [InlineData(data: [null, 0])]
    public void ToSeconds_ParsesTimecode(string? input, int expected)
    {
        input.ToSeconds().Should().Be(expected: expected);
    }

    [Fact]
    public void TitleSort_StripsLeadingArticle()
    {
        "The Matrix".TitleSort(parseYear: (int?)null).Should().Be(expected: "matrix");
        "An Apple".TitleSort(parseYear: (int?)null).Should().Be(expected: "apple");
        "A Bridge".TitleSort(parseYear: (int?)null).Should().Be(expected: "bridge");
    }

    [Fact]
    public void TitleSort_InjectsYearAtColonSeparator()
    {
        "Title: Sub".TitleSort(parseYear: 2020).Should().Be(expected: "title.2020.sub");
    }

    [Theory]
    [InlineData(data: ["HelloWorld", "hello_world"])]
    [InlineData(data: ["ID", "i_d"])]
    [InlineData(data: ["already_snake", "already_snake"])]
    public void ToSnakeCase_InsertsUnderscores(string input, string expected)
    {
        input.ToSnakeCase().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hello world", "Hello_World"])]
    [InlineData(data: ["a b c", "A_B_C"])]
    public void ToPascalCase_TitleCasesAndJoinsWithUnderscore(string input, string expected)
    {
        input.ToPascalCase().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["hello world", "Hello World"])]
    [InlineData(data: ["THE QUICK", "The Quick"])]
    public void ToTitleCase_CapitalizesEachWord(string input, string expected)
    {
        input.ToTitleCase().Should().Be(expected: expected);
    }

    [Fact]
    public void Capitalize_UppercasesFirstCharOnly()
    {
        "hello".Capitalize().Should().Be(expected: "Hello");
        "hELLO".Capitalize().Should().Be(expected: "HELLO");
    }

    [Fact]
    public void ToUcFirst_UppercasesFirstLowercasesRest()
    {
        "hELLO".ToUcFirst().Should().Be(expected: "Hello");
    }

    [Fact]
    public void SanitizeFileName_ReplacesSmartQuotesWithAscii()
    {
        "movie’s tale.mkv".SanitizeFileName().Should().Be(expected: "movie's tale.mkv");
    }

    [Theory]
    [InlineData(data: ["12.7", 13])]
    [InlineData(data: ["12", 12])]
    [InlineData(data: ["", 0])]
    [InlineData(data: ["abc", 0])]
    public void ToInt_RoundsOrZero(string input, int expected)
    {
        input.ToInt().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["1.5", 1.5])]
    [InlineData(data: ["", 0.0])]
    [InlineData(data: ["nope", 0.0])]
    public void ToDouble_ParsesOrZero(string input, double expected)
    {
        input.ToDouble().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["123", 123L])]
    [InlineData(data: ["", 0L])]
    [InlineData(data: ["x", 0L])]
    public void ToLong_ParsesOrZero(string input, long expected)
    {
        input.ToLong().Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["true", true])]
    [InlineData(data: ["false", false])]
    [InlineData(data: ["", false])]
    [InlineData(data: ["yes", false])] // only bool.TryParse-able strings count
    public void ToBoolean_ParsesOrFalse(string input, bool expected)
    {
        input.ToBoolean().Should().Be(expected: expected);
    }

    [Fact]
    public void ContainsSanitized_IgnoresDiacriticsAndCase()
    {
        "Café Royale".ContainsSanitized(value: "cafe").Should().BeTrue();
        "abc".ContainsSanitized(value: null).Should().BeFalse();
    }

    [Fact]
    public void ContainsSanitized_NonAsciiNeedle_DoesNotFalseMatchUnrelatedText()
    {
        // CJK/Cyrillic/Greek sanitize down to an empty string under the
        // ASCII-only regex; Contains("") must not make this trivially true.
        "The Matrix".ContainsSanitized(value: "こんにちは").Should().BeFalse();
        "Some Album".ContainsSanitized(value: "Привет").Should().BeFalse();
        "こんにちは".ContainsSanitized(value: "hello").Should().BeFalse();
    }

    [Fact]
    public void ContainsSanitized_NonAsciiNeedle_StillMatchesRealContainment()
    {
        "こんにちは世界".ContainsSanitized(value: "こんにちは").Should().BeTrue();
        "Привет мир".ContainsSanitized(value: "Привет").Should().BeTrue();
    }

    [Fact]
    public void EqualsSanitized_MatchesAfterSanitization()
    {
        "The Matrix".EqualsSanitized(value: "the matrix").Should().BeTrue();
        "Alpha".EqualsSanitized(value: "Beta").Should().BeFalse();
    }

    [Fact]
    public void ToGuid_ReturnsEmptyOnMalformed()
    {
        "not-a-guid".ToGuid().Should().Be(expected: Guid.Empty);
        Guid valid = Guid.NewGuid();
        valid.ToString().ToGuid().Should().Be(expected: valid);
    }
}
