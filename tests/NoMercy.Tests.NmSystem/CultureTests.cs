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

/// <summary>
/// Pins <see cref="Culture"/>: ISO 639 code to English name resolution (with the
/// special und/mul/zxx labels and legacy bibliographic codes) and the
/// CultureInfo to bibliographic tag mapping used for subtitle/audio language tags.
/// </summary>
[Trait("Category", "Unit")]
public class CultureTests
{
    [Theory]
    [InlineData("en", "English")]
    [InlineData("nl", "Dutch")]
    [InlineData("de", "German")]
    [InlineData("fr", "French")]
    public void EnglishLanguageName_ResolvesCommonCodes(string code, string expected)
    {
        Culture.EnglishLanguageName(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("und", "Unknown")]
    [InlineData("mul", "Multiple Languages")]
    [InlineData("zxx", "No Language")]
    public void EnglishLanguageName_ResolvesSpecialCodes(string code, string expected)
    {
        Culture.EnglishLanguageName(code).Should().Be(expected);
    }

    [Fact]
    public void EnglishLanguageName_ResolvesLegacyBibliographicCode()
    {
        // "ger" is the bibliographic form of "deu" — both resolve to German.
        Culture.EnglishLanguageName("ger").Should().Be("German");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EnglishLanguageName_BlankCodeIsUnknown(string? code)
    {
        Culture.EnglishLanguageName(code!).Should().Be("Unknown");
    }

    [Fact]
    public void EnglishLanguageName_UnknownCodeUppercasesInput()
    {
        Culture.EnglishLanguageName("qqq").Should().Be("QQQ");
    }

    [Fact]
    public void EnglishLanguageTag_MapsToBibliographicCode()
    {
        new CultureInfo("nl").EnglishLanguageTag().Should().Be("dut");
        new CultureInfo("de").EnglishLanguageTag().Should().Be("ger");
    }

    [Fact]
    public void EnglishLanguageTag_KeepsEnglishAsEng()
    {
        new CultureInfo("en").EnglishLanguageTag().Should().Be("eng");
    }
}
