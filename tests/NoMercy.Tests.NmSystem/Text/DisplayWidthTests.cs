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
}
