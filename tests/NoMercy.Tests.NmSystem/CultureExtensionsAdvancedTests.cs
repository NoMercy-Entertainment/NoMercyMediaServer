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

namespace NoMercy.Tests.NmSystem;

[Trait("Category", "Unit")]
public class CultureExtensionsAdvancedTests
{
    [Fact]
    public void EnglishLanguageTag_WithEnglish_ReturnsEng()
    {
        CultureInfo culture = new("en-US");
        string result = culture.EnglishLanguageTag();
        result.Should().Be("eng");
    }

    [Fact]
    public void EnglishLanguageTag_WithGerman_ReturnsMappedLegacyCode()
    {
        CultureInfo culture = new("de-DE");
        string result = culture.EnglishLanguageTag();
        result.Should().Be("ger");
    }

    [Fact]
    public void EnglishLanguageTag_WithFrench_ReturnsMappedLegacyCode()
    {
        CultureInfo culture = new("fr-FR");
        string result = culture.EnglishLanguageTag();
        result.Should().Be("fre");
    }

    [Fact]
    public void EnglishLanguageTag_WithDutch_ReturnsMappedLegacyCode()
    {
        CultureInfo culture = new("nl-NL");
        string result = culture.EnglishLanguageTag();
        result.Should().Be("dut");
    }

    [Fact]
    public void EnglishLanguageTag_WithSpanish_ReturnsCurrentCode()
    {
        CultureInfo culture = new("es-ES");
        string result = culture.EnglishLanguageTag();
        result.Should().Be("spa");
    }

    [Theory]
    [InlineData(["en", "English"])]
    [InlineData(["eng", "English"])]
    [InlineData(["de", "German"])]
    [InlineData(["deu", "German"])]
    [InlineData(["fr", "French"])]
    [InlineData(["fra", "French"])]
    public void EnglishLanguageName_ReturnsNameForCode(string code, string expected)
    {
        string result = Culture.EnglishLanguageName(code);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(["und", "Unknown"])]
    [InlineData(["mul", "Multiple Languages"])]
    [InlineData(["zxx", "No Language"])]
    public void EnglishLanguageName_WithSpecialCodes_ReturnsLabel(string code, string expected)
    {
        string result = Culture.EnglishLanguageName(code);
        result.Should().Be(expected);
    }

    [Fact]
    public void EnglishLanguageName_WithNull_ReturnsUnknown()
    {
        string result = Culture.EnglishLanguageName(null!);
        result.Should().Be("Unknown");
    }

    [Fact]
    public void EnglishLanguageName_WithEmpty_ReturnsUnknown()
    {
        string result = Culture.EnglishLanguageName("");
        result.Should().Be("Unknown");
    }

    [Fact]
    public void EnglishLanguageName_WithWhitespace_ReturnsUnknown()
    {
        string result = Culture.EnglishLanguageName("   ");
        result.Should().Be("Unknown");
    }

    [Fact]
    public void EnglishLanguageName_WithUnknownCode_ReturnsUppercasedCode()
    {
        string result = Culture.EnglishLanguageName("xyz");
        result.Should().Be("XYZ");
    }

    [Fact]
    public void EnglishLanguageName_CaseInsensitive()
    {
        string resultLower = Culture.EnglishLanguageName("eng");
        string resultUpper = Culture.EnglishLanguageName("ENG");
        resultLower.Should().Be(resultUpper);
        resultLower.Should().Be("English");
    }
}
