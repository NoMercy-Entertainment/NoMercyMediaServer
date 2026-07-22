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
public class TitleSortHelperTests
{
    [Theory]
    [InlineData(data: ["The Matrix", null, "matrix"])]
    [InlineData(data: ["An Apple", null, "apple"])]
    [InlineData(data: ["A Bridge", null, "bridge"])]
    [InlineData(data: ["the matrix", null, "matrix"])]
    [InlineData(data: ["THE MATRIX", null, "matrix"])]
    public void TitleSort_StripLeadingArticles(string title, int? year, string expected)
    {
        string result = title.TitleSort(parseYear: year);
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["Matrix", null, "matrix"])]
    [InlineData(data: ["Bridge", null, "bridge"])]
    [InlineData(data: ["Zebra", null, "zebra"])]
    public void TitleSort_NoArticle_JustLowercase(string title, int? year, string expected)
    {
        string result = title.TitleSort(parseYear: year);
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void TitleSort_WithYear_ContainsYearAndTitle()
    {
        string result = "Title: Subtitle".TitleSort(parseYear: 2020);
        result.Should().Contain(expected: "title");
        result.Should().Contain(expected: "2020");
    }

    [Fact]
    public void TitleSort_WithYearNoColon_JustLowercase()
    {
        string result = "Title".TitleSort(parseYear: 2020);
        result.Should().Be(expected: "title");
    }

    [Fact]
    public void TitleSort_WithNullYear_IgnoresYear()
    {
        string result = "Title: Subtitle".TitleSort(date: null);
        result.Should().Contain(expected: "title");
    }

    [Fact]
    public void TitleSort_ArticleTheWithColon()
    {
        string result = "The Title: Subtitle".TitleSort(parseYear: 2020);
        result.Should().Contain(expected: "title");
        result.Should().Contain(expected: "2020");
    }

    [Fact]
    public void TitleSort_ArticleAnWithColon()
    {
        string result = "An Apple: Tree".TitleSort(parseYear: 2021);
        result.Should().Contain(expected: "apple");
        result.Should().Contain(expected: "2021");
    }

    [Theory]
    [InlineData(data: ["", null])]
    [InlineData(data: ["", 2020])]
    public void TitleSort_EmptyTitle_ReturnsEmpty(string title, int? year)
    {
        string result = title.TitleSort(parseYear: year);
        result.Should().Be(expected: "");
    }

    [Fact]
    public void TitleSort_WithValidYear_InjectsYear()
    {
        string result = "Title: Subtitle".TitleSort(parseYear: 2000);
        result.Should().Contain(expected: "2000");
    }

    [Fact]
    public void TitleSort_WithDatetime_UsesYearFromDate()
    {
        DateTime date = new(year: 2019, month: 6, day: 15);
        string result = "Film: Movie".TitleSort(date: date);
        result.Should().Contain(expected: "2019");
    }

    [Fact]
    public void TitleSort_LowercasesResult()
    {
        string result = "Movie!Test".TitleSort(date: null);
        result.Should().Be(expected: result.ToLowerInvariant());
    }
}
