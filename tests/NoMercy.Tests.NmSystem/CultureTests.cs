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
[Trait(name: "Category", value: "Unit")]
public class CultureTests
{
    [Theory]
    [InlineData(data: ["en", "English"])]
    [InlineData(data: ["nl", "Dutch"])]
    [InlineData(data: ["de", "German"])]
    [InlineData(data: ["fr", "French"])]
    public void EnglishLanguageName_ResolvesCommonCodes(string code, string expected)
    {
        Culture.EnglishLanguageName(code: code).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["und", "Unknown"])]
    [InlineData(data: ["mul", "Multiple Languages"])]
    [InlineData(data: ["zxx", "No Language"])]
    public void EnglishLanguageName_ResolvesSpecialCodes(string code, string expected)
    {
        Culture.EnglishLanguageName(code: code).Should().Be(expected: expected);
    }

    [Fact]
    public void EnglishLanguageName_ResolvesLegacyBibliographicCode()
    {
        // "ger" is the bibliographic form of "deu" — both resolve to German.
        Culture.EnglishLanguageName(code: "ger").Should().Be(expected: "German");
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    [InlineData(data: null)]
    public void EnglishLanguageName_BlankCodeIsUnknown(string? code)
    {
        Culture.EnglishLanguageName(code: code!).Should().Be(expected: "Unknown");
    }

    [Fact]
    public void EnglishLanguageName_UnknownCodeUppercasesInput()
    {
        Culture.EnglishLanguageName(code: "qqq").Should().Be(expected: "QQQ");
    }

    [Fact]
    public void EnglishLanguageTag_MapsToBibliographicCode()
    {
        new CultureInfo(name: "nl").EnglishLanguageTag().Should().Be(expected: "dut");
        new CultureInfo(name: "de").EnglishLanguageTag().Should().Be(expected: "ger");
    }

    [Fact]
    public void EnglishLanguageTag_KeepsEnglishAsEng()
    {
        new CultureInfo(name: "en").EnglishLanguageTag().Should().Be(expected: "eng");
    }

    [Theory]
    [InlineData(data: ["nl", "dut"])]
    [InlineData(data: ["nld", "dut"])]
    [InlineData(data: ["nl-NL", "dut"])]
    [InlineData(data: ["de", "ger"])]
    [InlineData(data: ["fr", "fre"])]
    [InlineData(data: ["cs", "cze"])]
    public void BibliographicLanguageCode_MapsToTheCodeOpenSubtitlesAccepts(
        string code,
        string expected
    )
    {
        Culture.BibliographicLanguageCode(code: code).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "dut")]
    [InlineData(data: "ger")]
    [InlineData(data: "eng")]
    [InlineData(data: "jpn")]
    public void BibliographicLanguageCode_IsIdempotent(string code)
    {
        Culture.BibliographicLanguageCode(code: code).Should().Be(expected: code);
        Culture
            .BibliographicLanguageCode(code: Culture.BibliographicLanguageCode(code: code))
            .Should()
            .Be(expected: code);
    }

    [Theory]
    [InlineData(data: ["en", "eng"])]
    [InlineData(data: ["ja", "jpn"])]
    [InlineData(data: ["hu", "hun"])]
    public void BibliographicLanguageCode_LeavesNonLegacyCodesOnTheirIso3Form(
        string code,
        string expected
    )
    {
        Culture.BibliographicLanguageCode(code: code).Should().Be(expected: expected);
    }

    [Fact]
    public void BibliographicLanguageCode_PassesThroughAnUnknownCode()
    {
        Culture.BibliographicLanguageCode(code: "zzz").Should().Be(expected: "zzz");
    }
}
