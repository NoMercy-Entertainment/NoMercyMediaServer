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
public class TitleSortHelperTests
{
    [Theory]
    [InlineData("The Matrix", null, "matrix")]
    [InlineData("An Apple", null, "apple")]
    [InlineData("A Bridge", null, "bridge")]
    [InlineData("the matrix", null, "matrix")]
    [InlineData("THE MATRIX", null, "matrix")]
    public void TitleSort_StripLeadingArticles(string title, int? year, string expected)
    {
        string result = title.TitleSort(year);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Matrix", null, "matrix")]
    [InlineData("Bridge", null, "bridge")]
    [InlineData("Zebra", null, "zebra")]
    public void TitleSort_NoArticle_JustLowercase(string title, int? year, string expected)
    {
        string result = title.TitleSort(year);
        result.Should().Be(expected);
    }

    [Fact]
    public void TitleSort_WithYear_ContainsYearAndTitle()
    {
        string result = "Title: Subtitle".TitleSort(2020);
        result.Should().Contain("title");
        result.Should().Contain("2020");
    }

    [Fact]
    public void TitleSort_WithYearNoColon_JustLowercase()
    {
        string result = "Title".TitleSort(2020);
        result.Should().Be("title");
    }

    [Fact]
    public void TitleSort_WithNullYear_IgnoresYear()
    {
        string result = "Title: Subtitle".TitleSort(null);
        result.Should().Contain("title");
    }

    [Fact]
    public void TitleSort_ArticleTheWithColon()
    {
        string result = "The Title: Subtitle".TitleSort(2020);
        result.Should().Contain("title");
        result.Should().Contain("2020");
    }

    [Fact]
    public void TitleSort_ArticleAnWithColon()
    {
        string result = "An Apple: Tree".TitleSort(2021);
        result.Should().Contain("apple");
        result.Should().Contain("2021");
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("", 2020)]
    public void TitleSort_EmptyTitle_ReturnsEmpty(string title, int? year)
    {
        string result = title.TitleSort(year);
        result.Should().Be("");
    }

    [Fact]
    public void TitleSort_WithValidYear_InjectsYear()
    {
        string result = "Title: Subtitle".TitleSort(2000);
        result.Should().Contain("2000");
    }

    [Fact]
    public void TitleSort_WithDatetime_UsesYearFromDate()
    {
        DateTime date = new(2019, 6, 15);
        string result = "Film: Movie".TitleSort(date);
        result.Should().Contain("2019");
    }

    [Fact]
    public void TitleSort_LowercasesResult()
    {
        string result = "Movie!Test".TitleSort(null);
        result.Should().Be(result.ToLowerInvariant());
    }
}
