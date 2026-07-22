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

using NoMercy.NmSystem.Text;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins <see cref="DisplayWidth"/>: ANSI escapes and combining marks are zero
/// cells, CJK/fullwidth/emoji are two, and padding/truncation/wrapping operate on
/// display cells rather than UTF-16 length.
/// </summary>
[Trait("Category", "Unit")]
public class DisplayWidthTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("héllo", 5)]
    public void Of_CountsLatin(string input, int expected)
    {
        DisplayWidth.Of(input).Should().Be(expected);
    }

    [Fact]
    public void Of_Null_IsZero()
    {
        DisplayWidth.Of(null).Should().Be(0);
    }

    [Theory]
    [InlineData("日本語", 6)]
    [InlineData("ＡＢＣ", 6)]
    public void Of_CountsWideAsTwo(string input, int expected)
    {
        DisplayWidth.Of(input).Should().Be(expected);
    }

    [Fact]
    public void Of_Emoji_IsTwo()
    {
        DisplayWidth.Of("\U0001F600").Should().Be(2);
    }

    [Fact]
    public void Of_CombiningMark_IsZeroWidth()
    {
        DisplayWidth.Of("é").Should().Be(1);
    }

    [Fact]
    public void Of_AnsiEscapes_AreZeroWidth()
    {
        DisplayWidth.Of("a[31mb[0m").Should().Be(2);
    }

    [Fact]
    public void Of_ZwjEmojiSequence_CountsCellsOnly()
    {
        DisplayWidth.Of("\U0001F469‍\U0001F4BB").Should().Be(4);
    }

    [Fact]
    public void PadRight_FillsToDisplayWidth()
    {
        DisplayWidth.PadRight("ab", 5).Should().Be("ab   ");
    }

    [Fact]
    public void PadLeft_AccountsForWideChars()
    {
        string padded = DisplayWidth.PadLeft("日", 6);
        DisplayWidth.Of(padded).Should().Be(6);
    }

    [Fact]
    public void Truncate_StaysWithinBudget()
    {
        string result = DisplayWidth.Truncate("abcdef", 4);
        DisplayWidth.Of(result).Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public void Wrap_ProducesLinesWithinWidth()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap("the quick brown fox jumps", 9);
        lines.Should().OnlyContain(line => DisplayWidth.Of(line) <= 9);
    }

    [Fact]
    public void Truncate_WithEmptyString_ReturnsEmpty()
    {
        string result = DisplayWidth.Truncate("", 10);
        result.Should().Be("");
    }

    [Fact]
    public void Truncate_WithNullString_ReturnsEmpty()
    {
        string result = DisplayWidth.Truncate(null, 10);
        result.Should().Be("");
    }

    [Fact]
    public void Truncate_WithZeroWidth_ReturnsEmpty()
    {
        string result = DisplayWidth.Truncate("hello", 0);
        result.Should().Be("");
    }

    [Fact]
    public void Truncate_WithShortString_ReturnsUnchanged()
    {
        string result = DisplayWidth.Truncate("hi", 10);
        result.Should().Be("hi");
    }

    [Fact]
    public void Truncate_WithLongString_AddsEllipsis()
    {
        string result = DisplayWidth.Truncate("hello world test", 10);
        result.Should().EndWith("…");
    }

    [Fact]
    public void Truncate_WithCustomEllipsis()
    {
        string result = DisplayWidth.Truncate("hello world test", 10, "...");
        result.Should().EndWith("...");
    }

    [Fact]
    public void Truncate_WithAnsiEscapesPreserved()
    {
        string colored = "a\x1b[31mred\x1b[0m";
        string result = DisplayWidth.Truncate(colored, 4);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Truncate_WithWideCharacters()
    {
        string result = DisplayWidth.Truncate("日本語test", 5);
        DisplayWidth.Of(result).Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void Wrap_WithEmptyString_ReturnsEmptyLine()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap("", 10);
        lines.Should().HaveCount(1);
        lines[0].Should().Be("");
    }

    [Fact]
    public void Wrap_WithNullString_ReturnsEmptyLine()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(null, 10);
        lines.Should().HaveCount(1);
        lines[0].Should().Be("");
    }

    [Fact]
    public void Wrap_WithZeroWidth_ReturnsWholeLine()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap("hello", 0);
        lines.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Wrap_WithSingleLongWord()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap("supercalifragilisticexpialidocious", 10);
        lines.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Wrap_WithWideCharacters()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap("日本語 テスト", 6);
        lines.Should().OnlyContain(line => DisplayWidth.Of(line) <= 6);
    }

    [Theory]
    [InlineData("http://example.com/a/b/c?d=e&f=g")]
    [InlineData("https://example.com/a/b/c?d=e&f=g")]
    public void Wrap_WithLongUrl_KeepsItOnOneUnbrokenLine(string url)
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(url, 10);
        lines.Should().ContainSingle().Which.Should().Be(url);
    }

    [Fact]
    public void Wrap_WithLongUrlAmongWords_DoesNotSplitTheUrl()
    {
        string url = "https://example.com/very/long/path?query=value&other=thing";
        IReadOnlyList<string> lines = DisplayWidth.Wrap($"see {url} for details", 10);
        lines.Should().ContainSingle(line => line == url);
    }

    [Fact]
    public void PadRight_WithNull_ReturnsSpaces()
    {
        string result = DisplayWidth.PadRight(null, 5);
        result.Should().Be("     ");
    }

    [Fact]
    public void PadRight_WithNegativePadding_ReturnsText()
    {
        string result = DisplayWidth.PadRight("hello", -5);
        result.Should().Be("hello");
    }

    [Fact]
    public void PadRight_WithWideCharacters()
    {
        string result = DisplayWidth.PadRight("日", 4);
        DisplayWidth.Of(result).Should().Be(4);
    }

    [Fact]
    public void PadLeft_WithNull_ReturnsSpaces()
    {
        string result = DisplayWidth.PadLeft(null, 5);
        result.Should().Be("     ");
    }

    [Fact]
    public void PadLeft_WithNegativePadding_ReturnsText()
    {
        string result = DisplayWidth.PadLeft("hello", -5);
        result.Should().Be("hello");
    }

    [Fact]
    public void PadLeft_WithWideCharacters()
    {
        string result = DisplayWidth.PadLeft("日", 4);
        DisplayWidth.Of(result).Should().Be(4);
    }

    [Fact]
    public void Of_WithControlCharacters_IsZeroWidth()
    {
        DisplayWidth.Of("\x00\x01\x02").Should().Be(0);
    }

    [Fact]
    public void Of_WithMixedContent()
    {
        DisplayWidth.Of("a日b").Should().Be(4);
    }
}
