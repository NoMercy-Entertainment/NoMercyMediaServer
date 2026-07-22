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
[Trait(name: "Category", value: "Unit")]
public class DisplayWidthTests
{
    [Theory]
    [InlineData(data: ["", 0])]
    [InlineData(data: ["abc", 3])]
    [InlineData(data: ["héllo", 5])]
    public void Of_CountsLatin(string input, int expected)
    {
        DisplayWidth.Of(text: input).Should().Be(expected: expected);
    }

    [Fact]
    public void Of_Null_IsZero()
    {
        DisplayWidth.Of(text: null).Should().Be(expected: 0);
    }

    [Theory]
    [InlineData(data: ["日本語", 6])]
    [InlineData(data: ["ＡＢＣ", 6])]
    public void Of_CountsWideAsTwo(string input, int expected)
    {
        DisplayWidth.Of(text: input).Should().Be(expected: expected);
    }

    [Fact]
    public void Of_Emoji_IsTwo()
    {
        DisplayWidth.Of(text: "\U0001F600").Should().Be(expected: 2);
    }

    [Fact]
    public void Of_CombiningMark_IsZeroWidth()
    {
        DisplayWidth.Of(text: "é").Should().Be(expected: 1);
    }

    [Fact]
    public void Of_AnsiEscapes_AreZeroWidth()
    {
        DisplayWidth.Of(text: "a[31mb[0m").Should().Be(expected: 2);
    }

    [Fact]
    public void Of_ZwjEmojiSequence_CountsCellsOnly()
    {
        DisplayWidth.Of(text: "\U0001F469‍\U0001F4BB").Should().Be(expected: 4);
    }

    [Fact]
    public void PadRight_FillsToDisplayWidth()
    {
        DisplayWidth.PadRight(text: "ab", width: 5).Should().Be(expected: "ab   ");
    }

    [Fact]
    public void PadLeft_AccountsForWideChars()
    {
        string padded = DisplayWidth.PadLeft(text: "日", width: 6);
        DisplayWidth.Of(text: padded).Should().Be(expected: 6);
    }

    [Fact]
    public void Truncate_StaysWithinBudget()
    {
        string result = DisplayWidth.Truncate(text: "abcdef", maxWidth: 4);
        DisplayWidth.Of(text: result).Should().BeLessThanOrEqualTo(expected: 4);
    }

    [Fact]
    public void Wrap_ProducesLinesWithinWidth()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: "the quick brown fox jumps", width: 9);
        lines.Should().OnlyContain(predicate: line => DisplayWidth.Of(line) <= 9);
    }

    [Fact]
    public void Truncate_WithEmptyString_ReturnsEmpty()
    {
        string result = DisplayWidth.Truncate(text: "", maxWidth: 10);
        result.Should().Be(expected: "");
    }

    [Fact]
    public void Truncate_WithNullString_ReturnsEmpty()
    {
        string result = DisplayWidth.Truncate(text: null, maxWidth: 10);
        result.Should().Be(expected: "");
    }

    [Fact]
    public void Truncate_WithZeroWidth_ReturnsEmpty()
    {
        string result = DisplayWidth.Truncate(text: "hello", maxWidth: 0);
        result.Should().Be(expected: "");
    }

    [Fact]
    public void Truncate_WithShortString_ReturnsUnchanged()
    {
        string result = DisplayWidth.Truncate(text: "hi", maxWidth: 10);
        result.Should().Be(expected: "hi");
    }

    [Fact]
    public void Truncate_WithLongString_AddsEllipsis()
    {
        string result = DisplayWidth.Truncate(text: "hello world test", maxWidth: 10);
        result.Should().EndWith(expected: "…");
    }

    [Fact]
    public void Truncate_WithCustomEllipsis()
    {
        string result = DisplayWidth.Truncate(text: "hello world test", maxWidth: 10, ellipsis: "...");
        result.Should().EndWith(expected: "...");
    }

    [Fact]
    public void Truncate_WithAnsiEscapesPreserved()
    {
        string colored = "a\x1b[31mred\x1b[0m";
        string result = DisplayWidth.Truncate(text: colored, maxWidth: 4);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Truncate_WithWideCharacters()
    {
        string result = DisplayWidth.Truncate(text: "日本語test", maxWidth: 5);
        DisplayWidth.Of(text: result).Should().BeLessThanOrEqualTo(expected: 5);
    }

    [Fact]
    public void Wrap_WithEmptyString_ReturnsEmptyLine()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: "", width: 10);
        lines.Should().HaveCount(expected: 1);
        lines[index: 0].Should().Be(expected: "");
    }

    [Fact]
    public void Wrap_WithNullString_ReturnsEmptyLine()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: null, width: 10);
        lines.Should().HaveCount(expected: 1);
        lines[index: 0].Should().Be(expected: "");
    }

    [Fact]
    public void Wrap_WithZeroWidth_ReturnsWholeLine()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: "hello", width: 0);
        lines.Should().HaveCountGreaterThanOrEqualTo(expected: 1);
    }

    [Fact]
    public void Wrap_WithSingleLongWord()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: "supercalifragilisticexpialidocious", width: 10);
        lines.Should().HaveCountGreaterThan(expected: 1);
    }

    [Fact]
    public void Wrap_WithWideCharacters()
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: "日本語 テスト", width: 6);
        lines.Should().OnlyContain(predicate: line => DisplayWidth.Of(line) <= 6);
    }

    [Theory]
    [InlineData(data: "http://example.com/a/b/c?d=e&f=g")]
    [InlineData(data: "https://example.com/a/b/c?d=e&f=g")]
    public void Wrap_WithLongUrl_KeepsItOnOneUnbrokenLine(string url)
    {
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: url, width: 10);
        lines.Should().ContainSingle().Which.Should().Be(expected: url);
    }

    [Fact]
    public void Wrap_WithLongUrlAmongWords_DoesNotSplitTheUrl()
    {
        string url = "https://example.com/very/long/path?query=value&other=thing";
        IReadOnlyList<string> lines = DisplayWidth.Wrap(text: $"see {url} for details", width: 10);
        lines.Should().ContainSingle(predicate: line => line == url);
    }

    [Fact]
    public void PadRight_WithNull_ReturnsSpaces()
    {
        string result = DisplayWidth.PadRight(text: null, width: 5);
        result.Should().Be(expected: "     ");
    }

    [Fact]
    public void PadRight_WithNegativePadding_ReturnsText()
    {
        string result = DisplayWidth.PadRight(text: "hello", width: -5);
        result.Should().Be(expected: "hello");
    }

    [Fact]
    public void PadRight_WithWideCharacters()
    {
        string result = DisplayWidth.PadRight(text: "日", width: 4);
        DisplayWidth.Of(text: result).Should().Be(expected: 4);
    }

    [Fact]
    public void PadLeft_WithNull_ReturnsSpaces()
    {
        string result = DisplayWidth.PadLeft(text: null, width: 5);
        result.Should().Be(expected: "     ");
    }

    [Fact]
    public void PadLeft_WithNegativePadding_ReturnsText()
    {
        string result = DisplayWidth.PadLeft(text: "hello", width: -5);
        result.Should().Be(expected: "hello");
    }

    [Fact]
    public void PadLeft_WithWideCharacters()
    {
        string result = DisplayWidth.PadLeft(text: "日", width: 4);
        DisplayWidth.Of(text: result).Should().Be(expected: 4);
    }

    [Fact]
    public void Of_WithControlCharacters_IsZeroWidth()
    {
        DisplayWidth.Of(text: "\x00\x01\x02").Should().Be(expected: 0);
    }

    [Fact]
    public void Of_WithMixedContent()
    {
        DisplayWidth.Of(text: "a日b").Should().Be(expected: 4);
    }
}
