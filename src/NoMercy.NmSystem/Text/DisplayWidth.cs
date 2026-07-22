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
        if (string.IsNullOrEmpty(value: text))
            return 0;

        ReadOnlySpan<char> span = text.AsSpan();
        int width = 0;
        int index = 0;
        while (index < span.Length)
        {
            if (span[index: index] == '')
            {
                int escapeLength = EscapeLength(span: span[index..]);
                if (escapeLength > 0)
                {
                    index += escapeLength;
                    continue;
                }
            }

            Rune.DecodeFromUtf16(source: span[index..], result: out Rune rune, charsConsumed: out int consumed);
            width += RuneWidth(rune: rune);
            index += consumed;
        }

        return width;
    }

    /// <summary>Pads <paramref name="text"/> on the right with spaces to <paramref name="width"/> cells.</summary>
    public static string PadRight(string? text, int width)
    {
        text ??= string.Empty;
        int padding = width - Of(text: text);
        return padding > 0 ? text + new string(c: ' ', count: padding) : text;
    }

    /// <summary>Pads <paramref name="text"/> on the left with spaces to <paramref name="width"/> cells.</summary>
    public static string PadLeft(string? text, int width)
    {
        text ??= string.Empty;
        int padding = width - Of(text: text);
        return padding > 0 ? new string(c: ' ', count: padding) + text : text;
    }

    /// <summary>
    /// Truncates <paramref name="text"/> to at most <paramref name="maxWidth"/> cells,
    /// appending <paramref name="ellipsis"/>. Escape sequences are preserved without
    /// counting toward the width.
    /// </summary>
    public static string Truncate(string? text, int maxWidth, string ellipsis = "…")
    {
        if (string.IsNullOrEmpty(value: text) || maxWidth <= 0)
            return string.Empty;
        if (Of(text: text) <= maxWidth)
            return text;

        int budget = maxWidth - Of(text: ellipsis);
        if (budget < 0)
            budget = 0;

        ReadOnlySpan<char> span = text.AsSpan();
        StringBuilder builder = new();
        int used = 0;
        int index = 0;
        while (index < span.Length)
        {
            if (span[index: index] == '')
            {
                int escapeLength = EscapeLength(span: span[index..]);
                if (escapeLength > 0)
                {
                    builder.Append(value: span.Slice(start: index, length: escapeLength));
                    index += escapeLength;
                    continue;
                }
            }

            Rune.DecodeFromUtf16(source: span[index..], result: out Rune rune, charsConsumed: out int consumed);
            int runeWidth = RuneWidth(rune: rune);
            if (used + runeWidth > budget)
                break;

            builder.Append(value: span.Slice(start: index, length: consumed));
            used += runeWidth;
            index += consumed;
        }

        builder.Append(value: ellipsis);
        return builder.ToString();
    }

    /// <summary>Greedy word-wrap to lines of at most <paramref name="width"/> cells.</summary>
    public static IReadOnlyList<string> Wrap(string? text, int width)
    {
        List<string> lines = new();
        if (string.IsNullOrEmpty(value: text) || width <= 0)
        {
            lines.Add(item: text ?? string.Empty);
            return lines;
        }

        StringBuilder current = new();
        int currentWidth = 0;
        foreach (string word in text.Split(separator: ' '))
        {
            int wordWidth = Of(text: word);
            if (wordWidth > width)
            {
                if (currentWidth > 0)
                {
                    lines.Add(item: current.ToString());
                    current.Clear();
                    currentWidth = 0;
                }

                // Hard-splitting a URL inserts a gutter/newline mid-string, which
                // breaks terminal hyperlink auto-detection and corrupts copy-paste.
                // Let it overflow the column as one unbroken line instead — the
                // terminal soft-wraps it visually without touching the real text.
                if (LooksLikeUrl(word: word))
                {
                    lines.Add(item: word);
                }
                else
                {
                    foreach (string piece in HardSplit(word: word, width: width))
                        lines.Add(item: piece);
                }
                continue;
            }

            int needed = currentWidth == 0 ? wordWidth : wordWidth + 1;
            if (currentWidth > 0 && currentWidth + needed > width)
            {
                lines.Add(item: current.ToString());
                current.Clear();
                currentWidth = 0;
            }

            if (currentWidth > 0)
            {
                current.Append(value: ' ');
                currentWidth += 1;
            }

            current.Append(value: word);
            currentWidth += wordWidth;
        }

        if (currentWidth > 0 || lines.Count == 0)
            lines.Add(item: current.ToString());

        return lines;
    }

    private static bool LooksLikeUrl(string word) =>
        word.StartsWith(value: "http://", comparisonType: StringComparison.Ordinal)
        || word.StartsWith(value: "https://", comparisonType: StringComparison.Ordinal);

    private static IEnumerable<string> HardSplit(string word, int width)
    {
        List<string> pieces = new();
        ReadOnlySpan<char> span = word.AsSpan();
        StringBuilder builder = new();
        int builderWidth = 0;
        int index = 0;
        while (index < span.Length)
        {
            Rune.DecodeFromUtf16(source: span[index..], result: out Rune rune, charsConsumed: out int consumed);
            int runeWidth = RuneWidth(rune: rune);
            if (builderWidth > 0 && builderWidth + runeWidth > width)
            {
                pieces.Add(item: builder.ToString());
                builder.Clear();
                builderWidth = 0;
            }

            builder.Append(value: span.Slice(start: index, length: consumed));
            builderWidth += runeWidth;
            index += consumed;
        }

        if (builderWidth > 0)
            pieces.Add(item: builder.ToString());

        return pieces;
    }

    private static int EscapeLength(ReadOnlySpan<char> span)
    {
        if (span.Length == 0 || span[index: 0] != '')
            return 0;

        if (span.Length >= 2 && span[index: 1] == '[')
        {
            int index = 2;
            while (index < span.Length && !(span[index: index] >= '@' && span[index: index] <= '~'))
                index++;
            return index < span.Length ? index + 1 : span.Length;
        }

        if (span.Length >= 2 && span[index: 1] == ']')
        {
            int index = 2;
            while (index < span.Length && span[index: index] != '')
            {
                if (span[index: index] == '' && index + 1 < span.Length && span[index: index + 1] == '\\')
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
        if (value < 0x20 || value is >= 0x7f and < 0xa0)
            return 0;

        UnicodeCategory category = Rune.GetUnicodeCategory(value: rune);
        if (
            category == UnicodeCategory.NonSpacingMark
            || category == UnicodeCategory.EnclosingMark
            || category == UnicodeCategory.Format
        )
            return 0;

        return IsWide(value: value) ? 2 : 1;
    }

    private static bool IsWide(int value)
    {
        return value is >= 0x1100 and <= 0x115f
            || value == 0x2329
            || value == 0x232a
            || value is >= 0x2e80 and <= 0x303e
            || value is >= 0x3041 and <= 0x33ff
            || value is >= 0x3400 and <= 0x4dbf
            || value is >= 0x4e00 and <= 0x9fff
            || value is >= 0xa000 and <= 0xa4cf
            || value is >= 0xa960 and <= 0xa97f
            || value is >= 0xac00 and <= 0xd7a3
            || value is >= 0xf900 and <= 0xfaff
            || value is >= 0xfe10 and <= 0xfe19
            || value is >= 0xfe30 and <= 0xfe6f
            || value is >= 0xff00 and <= 0xff60
            || value is >= 0xffe0 and <= 0xffe6
            || value is >= 0x1b000 and <= 0x1b16f
            || value is >= 0x1f004 and <= 0x1f004
            || value is >= 0x1f0cf and <= 0x1f0cf
            || value is >= 0x1f18e and <= 0x1f18e
            || value is >= 0x1f191 and <= 0x1f19a
            || value is >= 0x1f200 and <= 0x1f251
            || value is >= 0x1f300 and <= 0x1f64f
            || value is >= 0x1f680 and <= 0x1f6ff
            || value is >= 0x1f900 and <= 0x1f9ff
            || value is >= 0x1fa70 and <= 0x1faff
            || value is >= 0x20000 and <= 0x3fffd;
    }
}
