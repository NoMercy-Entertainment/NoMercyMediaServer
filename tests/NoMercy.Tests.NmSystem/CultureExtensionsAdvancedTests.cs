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

using System.Globalization;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Tests.NmSystem;

[Trait(name: "Category", value: "Unit")]
public class CultureExtensionsAdvancedTests
{
    [Fact]
    public void EnglishLanguageTag_WithEnglish_ReturnsEng()
    {
        CultureInfo culture = new(name: "en-US");
        string result = culture.EnglishLanguageTag();
        result.Should().Be(expected: "eng");
    }

    [Fact]
    public void EnglishLanguageTag_WithGerman_ReturnsMappedLegacyCode()
    {
        CultureInfo culture = new(name: "de-DE");
        string result = culture.EnglishLanguageTag();
        result.Should().Be(expected: "ger");
    }

    [Fact]
    public void EnglishLanguageTag_WithFrench_ReturnsMappedLegacyCode()
    {
        CultureInfo culture = new(name: "fr-FR");
        string result = culture.EnglishLanguageTag();
        result.Should().Be(expected: "fre");
    }

    [Fact]
    public void EnglishLanguageTag_WithDutch_ReturnsMappedLegacyCode()
    {
        CultureInfo culture = new(name: "nl-NL");
        string result = culture.EnglishLanguageTag();
        result.Should().Be(expected: "dut");
    }

    [Fact]
    public void EnglishLanguageTag_WithSpanish_ReturnsCurrentCode()
    {
        CultureInfo culture = new(name: "es-ES");
        string result = culture.EnglishLanguageTag();
        result.Should().Be(expected: "spa");
    }

    [Theory]
    [InlineData(data: ["en", "English"])]
    [InlineData(data: ["eng", "English"])]
    [InlineData(data: ["de", "German"])]
    [InlineData(data: ["deu", "German"])]
    [InlineData(data: ["fr", "French"])]
    [InlineData(data: ["fra", "French"])]
    public void EnglishLanguageName_ReturnsNameForCode(string code, string expected)
    {
        string result = Culture.EnglishLanguageName(code: code);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["und", "Unknown"])]
    [InlineData(data: ["mul", "Multiple Languages"])]
    [InlineData(data: ["zxx", "No Language"])]
    public void EnglishLanguageName_WithSpecialCodes_ReturnsLabel(string code, string expected)
    {
        string result = Culture.EnglishLanguageName(code: code);
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void EnglishLanguageName_WithNull_ReturnsUnknown()
    {
        string result = Culture.EnglishLanguageName(code: null!);
        result.Should().Be(expected: "Unknown");
    }

    [Fact]
    public void EnglishLanguageName_WithEmpty_ReturnsUnknown()
    {
        string result = Culture.EnglishLanguageName(code: "");
        result.Should().Be(expected: "Unknown");
    }

    [Fact]
    public void EnglishLanguageName_WithWhitespace_ReturnsUnknown()
    {
        string result = Culture.EnglishLanguageName(code: "   ");
        result.Should().Be(expected: "Unknown");
    }

    [Fact]
    public void EnglishLanguageName_WithUnknownCode_ReturnsUppercasedCode()
    {
        string result = Culture.EnglishLanguageName(code: "xyz");
        result.Should().Be(expected: "XYZ");
    }

    [Fact]
    public void EnglishLanguageName_CaseInsensitive()
    {
        string resultLower = Culture.EnglishLanguageName(code: "eng");
        string resultUpper = Culture.EnglishLanguageName(code: "ENG");
        resultLower.Should().Be(expected: resultUpper);
        resultLower.Should().Be(expected: "English");
    }
}
