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
using System.Text;

namespace NoMercy.NmSystem.Text;

/// <summary>
/// Terminal display-width measurement: the number of monospace cells a string
/// occupies. ANSI/CSI escape sequences count as zero, combining marks and other
/// zero-width code points count as zero, East-Asian Wide/Fullwidth code points
/// (including most emoji) count as two, everything else counts as one. All
/// console column padding, truncation and wrapping must go through this so layout
/// never drifts on CJK titles, emoji, or already-coloured (escape-laden) strings.
/// </summary>
public static class DisplayWidth
{
    /// <summary>Display width, in terminal cells, of <paramref name="text"/>.</summary>
    public static int Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        ReadOnlySpan<char> span = text.AsSpan();
        int width = 0;
        int index = 0;
        while (index < span.Length)
        {
            if (span[index] == '')
            {
                int escapeLength = EscapeLength(span[index..]);
                if (escapeLength > 0)
                {
                    index += escapeLength;
                    continue;
                }
            }

            Rune.DecodeFromUtf16(span[index..], out Rune rune, out int consumed);
            width += RuneWidth(rune);
            index += consumed;
        }

        return width;
    }

    /// <summary>Pads <paramref name="text"/> on the right with spaces to <paramref name="width"/> cells.</summary>
    public static string PadRight(string? text, int width)
    {
        text ??= string.Empty;
        int padding = width - Of(text);
        return padding > 0 ? text + new string(' ', padding) : text;
    }

    /// <summary>Pads <paramref name="text"/> on the left with spaces to <paramref name="width"/> cells.</summary>
    public static string PadLeft(string? text, int width)
    {
        text ??= string.Empty;
        int padding = width - Of(text);
        return padding > 0 ? new string(' ', padding) + text : text;
    }

    /// <summary>
    /// Truncates <paramref name="text"/> to at most <paramref name="maxWidth"/> cells,
    /// appending <paramref name="ellipsis"/>. Escape sequences are preserved without
    /// counting toward the width.
    /// </summary>
    public static string Truncate(string? text, int maxWidth, string ellipsis = "…")
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return string.Empty;
        if (Of(text) <= maxWidth)
            return text;

        int budget = maxWidth - Of(ellipsis);
        if (budget < 0)
            budget = 0;

        ReadOnlySpan<char> span = text.AsSpan();
        StringBuilder builder = new();
        int used = 0;
        int index = 0;
        while (index < span.Length)
        {
            if (span[index] == '')
            {
                int escapeLength = EscapeLength(span[index..]);
                if (escapeLength > 0)
                {
                    builder.Append(span.Slice(index, escapeLength));
                    index += escapeLength;
                    continue;
                }
            }

            Rune.DecodeFromUtf16(span[index..], out Rune rune, out int consumed);
            int runeWidth = RuneWidth(rune);
            if (used + runeWidth > budget)
                break;

            builder.Append(span.Slice(index, consumed));
            used += runeWidth;
            index += consumed;
        }

        builder.Append(ellipsis);
        return builder.ToString();
    }

    /// <summary>Greedy word-wrap to lines of at most <paramref name="width"/> cells.</summary>
    public static IReadOnlyList<string> Wrap(string? text, int width)
    {
        List<string> lines = new();
        if (string.IsNullOrEmpty(text) || width <= 0)
        {
            lines.Add(text ?? string.Empty);
            return lines;
        }

        StringBuilder current = new();
        int currentWidth = 0;
        foreach (string word in text.Split(' '))
        {
            int wordWidth = Of(word);
            if (wordWidth > width)
            {
                if (currentWidth > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    currentWidth = 0;
                }

                foreach (string piece in HardSplit(word, width))
                    lines.Add(piece);
                continue;
            }

            int needed = currentWidth == 0 ? wordWidth : wordWidth + 1;
            if (currentWidth > 0 && currentWidth + needed > width)
            {
                lines.Add(current.ToString());
                current.Clear();
                currentWidth = 0;
            }

            if (currentWidth > 0)
            {
                current.Append(' ');
                currentWidth += 1;
            }

            current.Append(word);
            currentWidth += wordWidth;
        }

        if (currentWidth > 0 || lines.Count == 0)
            lines.Add(current.ToString());

        return lines;
    }

    private static IEnumerable<string> HardSplit(string word, int width)
    {
        List<string> pieces = new();
        ReadOnlySpan<char> span = word.AsSpan();
        StringBuilder builder = new();
        int builderWidth = 0;
        int index = 0;
        while (index < span.Length)
        {
            Rune.DecodeFromUtf16(span[index..], out Rune rune, out int consumed);
            int runeWidth = RuneWidth(rune);
            if (builderWidth > 0 && builderWidth + runeWidth > width)
            {
                pieces.Add(builder.ToString());
                builder.Clear();
                builderWidth = 0;
            }

            builder.Append(span.Slice(index, consumed));
            builderWidth += runeWidth;
            index += consumed;
        }

        if (builderWidth > 0)
            pieces.Add(builder.ToString());

        return pieces;
    }

    private static int EscapeLength(ReadOnlySpan<char> span)
    {
        if (span.Length == 0 || span[0] != '')
            return 0;

        if (span.Length >= 2 && span[1] == '[')
        {
            int index = 2;
            while (index < span.Length && !(span[index] >= '@' && span[index] <= '~'))
                index++;
            return index < span.Length ? index + 1 : span.Length;
        }

        if (span.Length >= 2 && span[1] == ']')
        {
            int index = 2;
            while (index < span.Length && span[index] != '')
            {
                if (span[index] == '' && index + 1 < span.Length && span[index + 1] == '\\')
                    return index + 2;
                index++;
            }
            return index < span.Length ? index + 1 : span.Length;
        }

        return 1;
    }

    private static int RuneWidth(Rune rune)
    {
        int value = rune.Value;
        if (value == 0)
            return 0;
        if (value < 0x20 || (value >= 0x7f && value < 0xa0))
            return 0;

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (
            category == UnicodeCategory.NonSpacingMark
            || category == UnicodeCategory.EnclosingMark
            || category == UnicodeCategory.Format
        )
            return 0;

        return IsWide(value) ? 2 : 1;
    }

    private static bool IsWide(int value)
    {
        return (value >= 0x1100 && value <= 0x115f)
            || value == 0x2329
            || value == 0x232a
            || (value >= 0x2e80 && value <= 0x303e)
            || (value >= 0x3041 && value <= 0x33ff)
            || (value >= 0x3400 && value <= 0x4dbf)
            || (value >= 0x4e00 && value <= 0x9fff)
            || (value >= 0xa000 && value <= 0xa4cf)
            || (value >= 0xa960 && value <= 0xa97f)
            || (value >= 0xac00 && value <= 0xd7a3)
            || (value >= 0xf900 && value <= 0xfaff)
            || (value >= 0xfe10 && value <= 0xfe19)
            || (value >= 0xfe30 && value <= 0xfe6f)
            || (value >= 0xff00 && value <= 0xff60)
            || (value >= 0xffe0 && value <= 0xffe6)
            || (value >= 0x1b000 && value <= 0x1b16f)
            || (value >= 0x1f004 && value <= 0x1f004)
            || (value >= 0x1f0cf && value <= 0x1f0cf)
            || (value >= 0x1f18e && value <= 0x1f18e)
            || (value >= 0x1f191 && value <= 0x1f19a)
            || (value >= 0x1f200 && value <= 0x1f251)
            || (value >= 0x1f300 && value <= 0x1f64f)
            || (value >= 0x1f680 && value <= 0x1f6ff)
            || (value >= 0x1f900 && value <= 0x1f9ff)
            || (value >= 0x1fa70 && value <= 0x1faff)
            || (value >= 0x20000 && value <= 0x3fffd);
    }
}
